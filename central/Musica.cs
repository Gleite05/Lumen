// O que está a tocar, pelas sessões de média do Windows (Spotify primeiro).
// Sem await: o csc do .NET Framework não liga o GetAwaiter às winmd do System32,
// por isso as operações WinRT são esperadas à mão (Esperar).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

class Musica
{
    readonly Action<object> publicar;
    GlobalSystemMediaTransportControlsSessionManager gestor;
    string ultimaChave = "", capa = "", ultimoEnvio = "";

    public Musica(Action<object> publicar) { this.publicar = publicar; }

    static T Esperar<T>(IAsyncOperation<T> op, int ms = 5000)
    {
        var t0 = Environment.TickCount;
        while (op.Status == AsyncStatus.Started)
        {
            if (Environment.TickCount - t0 > ms) { op.Cancel(); throw new TimeoutException(); }
            Thread.Sleep(10);
        }
        if (op.Status != AsyncStatus.Completed) throw new InvalidOperationException("WinRT: " + op.Status);
        return op.GetResults();
    }

    public void Iniciar()
    {
        new Thread(() =>
        {
            while (true)
            {
                try { Ler(); } catch (Exception e) { P.Log("música: " + e.Message); gestor = null; Thread.Sleep(5000); }
                Thread.Sleep(1500);
            }
        }) { IsBackground = true }.Start();
    }

    GlobalSystemMediaTransportControlsSession Sessao()
    {
        if (gestor == null) return null;
        var todas = gestor.GetSessions().ToList();
        return todas.FirstOrDefault(s => s.SourceAppUserModelId.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? gestor.GetCurrentSession();
    }

    void Ler()
    {
        if (gestor == null) gestor = Esperar(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
        var s = Sessao();
        Dictionary<string, object> d;
        if (s == null) d = new Dictionary<string, object> { { "tem", false } };
        else
        {
            var props = Esperar(s.TryGetMediaPropertiesAsync());
            var tl = s.GetTimelineProperties();
            var pb = s.GetPlaybackInfo();
            string chave = props.Title + "|" + props.Artist;
            if (chave != ultimaChave)
            {
                ultimaChave = chave; capa = "";
                if (props.Thumbnail != null)
                {
                    try
                    {
                        using (var st = Esperar(props.Thumbnail.OpenReadAsync()))
                        using (var rd = new DataReader(st))
                        {
                            uint n = Esperar(rd.LoadAsync((uint)Math.Min(st.Size, 4000000)));
                            var bytes = new byte[n];
                            rd.ReadBytes(bytes);
                            capa = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
                        }
                    }
                    catch { }
                }
            }
            double dur = (tl.EndTime - tl.StartTime).TotalSeconds, pos = tl.Position.TotalSeconds;
            bool aTocar = pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (aTocar) pos += (DateTimeOffset.Now - tl.LastUpdatedTime).TotalSeconds;
            d = new Dictionary<string, object> {
                { "tem", !string.IsNullOrEmpty(props.Title) }, { "titulo", props.Title ?? "" }, { "artista", props.Artist ?? "" },
                { "capa", capa }, { "aTocar", aTocar }, { "pos", Math.Round(Math.Max(0, Math.Min(pos, dur))) }, { "dur", Math.Round(dur) }
            };
        }
        string j = P.Json.Serialize(d);
        if (j != ultimoEnvio) { ultimoEnvio = j; publicar(d); }
    }

    public void Comando(string cmd)
    {
        new Thread(() =>
        {
            try
            {
                var s = Sessao();
                if (s == null) return;
                if (cmd == "play") Esperar(s.TryTogglePlayPauseAsync());
                else if (cmd == "next") Esperar(s.TrySkipNextAsync());
                else if (cmd == "prev") Esperar(s.TrySkipPreviousAsync());
            }
            catch (Exception e) { P.Log("média: " + e.Message); }
        }) { IsBackground = true }.Start();
    }
}
