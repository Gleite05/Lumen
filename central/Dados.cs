// Recolha dos dados mostrados nos painéis. Cada fonte corre na sua thread e envia
// mensagens {tipo: ...} para a página quando muda.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

class Dados
{
    readonly Action<object> enviar;
    volatile bool parar;
    readonly Dictionary<string, object> ultimo = new Dictionary<string, object>();
    readonly object trava = new object();
    readonly AutoResetEvent acordarVpn = new AutoResetEvent(false);
    string vpnLigada = "";
    DateTime vpnDesde = DateTime.MinValue;
    readonly Dictionary<string, string> vpnOcupada = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    Musica musica;

    public Dados(Action<object> enviar) { this.enviar = enviar; }

    public void Iniciar()
    {
        Publicar("atalhos", P.Lista("atalhos").Select(a => (object)new Dictionary<string, object> {
            { "nome", P.S(a, "nome") }, { "icone", P.S(a, "icone") }, { "acao", P.S(a, "acao") } }).ToArray());
        Ciclo("servidores", 30000, LerServidores);
        Ciclo("rede", 30000, LerRede);
        Ciclo("tempo", 300000, LerTempo);
        new Thread(CicloVpn) { IsBackground = true }.Start();
        var ag = new Thread(() => { Thread.Sleep(5000); while (!parar) { Publicar("agenda", LerAgenda()); Thread.Sleep(120000); } }) { IsBackground = true };
        ag.SetApartmentState(ApartmentState.STA);
        ag.Start();
        try { musica = new Musica(d => Publicar("musica", d)); musica.Iniciar(); }
        catch (Exception e) { P.Log("música: " + e.Message); }
    }

    public void Parar() { parar = true; }

    void Ciclo(string nome, int ms, Func<object> ler)
    {
        new Thread(() =>
        {
            while (!parar)
            {
                try { Publicar(nome, ler()); } catch (Exception e) { P.Log(nome + ": " + e.Message); }
                Thread.Sleep(ms);
            }
        }) { IsBackground = true }.Start();
    }

    void Publicar(string tipo, object dados)
    {
        if (dados == null) return;
        var msg = new Dictionary<string, object> { { "tipo", tipo }, { "dados", dados } };
        lock (trava) ultimo[tipo] = msg;
        enviar(msg);
    }

    public void EnviarTudo()
    {
        List<object> l;
        lock (trava) l = ultimo.Values.ToList();
        foreach (var m in l) enviar(m);
    }

    // ------------------------------------------------ servidores
    object LerServidores()
    {
        var lista = P.Lista("servidores").ToList();
        var res = new object[lista.Count];
        Parallel.For(0, lista.Count, i =>
        {
            var s = lista[i];
            long ms; bool ok = Testar(P.S(s, "host"), P.S(s, "porta"), out ms);
            res[i] = new Dictionary<string, object> { { "nome", P.S(s, "nome") }, { "sub", P.S(s, "sub") }, { "ok", ok }, { "ms", ok ? ms : -1 }, { "acao", P.S(s, "acao") } };
        });
        return res;
    }

    static bool Testar(string host, string porta, out long ms)
    {
        ms = -1;
        if (string.IsNullOrEmpty(host)) return false;
        try
        {
            using (var p = new Ping())
            {
                var r = p.Send(host, 1200);
                if (r.Status == IPStatus.Success) { ms = r.RoundtripTime; return true; }
            }
        }
        catch { }
        int po;
        if (int.TryParse(porta, out po) && po > 0)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using (var c = new TcpClient())
                {
                    var t = c.ConnectAsync(host, po);
                    if (t.Wait(1500) && c.Connected) { ms = sw.ElapsedMilliseconds; return true; }
                }
            }
            catch { }
        }
        return false;
    }

    // ------------------------------------------------ rede
    string ipPublico = "";
    DateTime ipQuando = DateTime.MinValue;
    object LerRede()
    {
        long ms; bool ok = Testar("1.1.1.1", "443", out ms);
        if ((DateTime.Now - ipQuando).TotalMinutes > 10 || ipPublico == "")
        {
            try
            {
                using (var w = new WebClient()) ipPublico = w.DownloadString("https://api.ipify.org").Trim();
                ipQuando = DateTime.Now;
            }
            catch { if (!ok) ipPublico = ""; }
        }
        return new Dictionary<string, object> { { "internet", ok }, { "ms", ms }, { "ip", ipPublico } };
    }

    // ------------------------------------------------ tempo (o mesmo do widget do Atrium)
    object LerTempo()
    {
        var f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Atrium\weather.json");
        if (!File.Exists(f)) return null;
        var d = P.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(f));
        int code = Convert.ToInt32(d["code"]);
        return new Dictionary<string, object> { { "cidade", P.S(d, "city") }, { "temp", Math.Round(Convert.ToDouble(d["temp"])) }, { "ceu", Ceu(code) } };
    }

    static string Ceu(int c)
    {
        if (c == 0) return "céu limpo";
        if (c <= 2) return "pouco nublado";
        if (c == 3) return "nublado";
        if (c == 45 || c == 48) return "nevoeiro";
        if (c >= 51 && c <= 57) return "chuvisco";
        if (c == 61 || c == 80) return "chuva fraca";
        if (c == 63 || c == 81) return "chuva";
        if (c == 65 || c == 82 || c == 66 || c == 67) return "chuva forte";
        if (c >= 71 && c <= 77 || c == 85 || c == 86) return "neve";
        if (c >= 95) return "trovoada";
        return "";
    }

    // ------------------------------------------------ VPN (RAS do Windows)
    void CicloVpn()
    {
        while (!parar)
        {
            try
            {
                var entradas = Ras.Entradas();
                var ativas = Ras.Ativas();
                string lig = ativas.FirstOrDefault() ?? "";
                Dictionary<string, string> oc;
                lock (vpnOcupada) oc = new Dictionary<string, string>(vpnOcupada);
                if (lig != vpnLigada)
                {
                    // caiu sem ter sido eu a desligar
                    if (lig == "" && vpnLigada != "" && !oc.ContainsKey(vpnLigada) && (DateTime.Now - vpnDesligueiEm).TotalSeconds > 30)
                        Avisar("VPN desligada", "A VPN «" + vpnLigada + "» caiu" + (vpnDesde != DateTime.MinValue ? " ao fim de " + Duracao(DateTime.Now - vpnDesde) : "") + ".");
                    vpnLigada = lig; vpnDesde = lig == "" ? DateTime.MinValue : DateTime.Now;
                }
                Publicar("vpn", new Dictionary<string, object> {
                    { "entradas", entradas.ToArray() }, { "ligada", vpnLigada },
                    { "desde", vpnDesde == DateTime.MinValue ? 0 : (long)(DateTime.Now - vpnDesde).TotalSeconds },
                    { "ocupada", oc }
                });
            }
            catch (Exception e) { P.Log("vpn: " + e.Message); }
            acordarVpn.WaitOne(5000);
        }
    }

    public Action<string, string> Avisar = (t, m) => { };
    DateTime vpnDesligueiEm = DateTime.MinValue;
    static string Duracao(TimeSpan t) { return t.TotalHours >= 1 ? (int)t.TotalHours + " h " + t.Minutes + " min" : t.Minutes + " min"; }

    public void Vpn(string nome, bool ligar)
    {
        if (string.IsNullOrEmpty(nome)) return;
        if (!ligar) vpnDesligueiEm = DateTime.Now;
        lock (vpnOcupada) vpnOcupada[nome] = ligar ? "a ligar…" : "a desligar…";
        acordarVpn.Set();
        new Thread(() =>
        {
            int r = ligar ? Ras.Rasdial("\"" + nome + "\"", 90000) : Ras.Rasdial("\"" + nome + "\" /disconnect", 20000);
            if (ligar && (r == 691 || r == 703 || r == 718)) { try { Process.Start("rasphone.exe", "-d \"" + nome + "\""); } catch { } }
            if (r != 0) P.Log("rasdial " + nome + " → " + r);
            lock (vpnOcupada) vpnOcupada.Remove(nome);
            acordarVpn.Set();
        }) { IsBackground = true }.Start();
    }

    // ------------------------------------------------ agenda do Outlook (só se já estiver aberto)
    object LerAgenda()
    {
        if (Process.GetProcessesByName("OUTLOOK").Length == 0)
            return new Dictionary<string, object> { { "aberto", false }, { "eventos", new object[0] } };
        var lista = new List<object>();
        try
        {
            dynamic app = Marshal.GetActiveObject("Outlook.Application");
            dynamic items = app.GetNamespace("MAPI").GetDefaultFolder(9).Items;
            items.IncludeRecurrences = true;
            items.Sort("[Start]");
            var hoje = DateTime.Today;
            string filtro = "[Start] >= '" + hoje.ToString("g") + "' AND [Start] < '" + hoje.AddDays(1).ToString("g") + "'";
            dynamic r = items.Restrict(filtro);
            int n = 0;
            foreach (dynamic it in r)
            {
                if (++n > 12) break;
                DateTime ini = it.Start, fim = it.End;
                if ((bool)it.AllDayEvent) continue;
                lista.Add(new Dictionary<string, object> {
                    { "ini", ini.ToString("HH:mm") }, { "fim", fim.ToString("HH:mm") },
                    { "a", (long)(ini - DateTime.Now).TotalMinutes }, { "b", (long)(fim - DateTime.Now).TotalMinutes },
                    { "titulo", (string)it.Subject ?? "" }, { "local", (string)it.Location ?? "" }
                });
            }
        }
        catch (Exception e) { P.Log("agenda: " + e.Message); }
        return new Dictionary<string, object> { { "aberto", true }, { "eventos", lista.ToArray() } };
    }

    // ------------------------------------------------ música
    public void Media(string cmd) { if (musica != null) musica.Comando(cmd); }
}

static class Ras
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    struct RASCONN
    {
        public int dwSize; public IntPtr hrasconn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string szEntryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)] public string szDeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] public string szDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szPhonebook;
        public int dwSubEntry; public Guid guidEntry; public int dwFlags; public long luid; public Guid guidCorrelationId;
    }
    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    static extern int RasEnumConnections([In, Out] RASCONN[] conns, ref int cb, ref int count);

    public static List<string> Ativas()
    {
        var l = new List<string>();
        int size = Marshal.SizeOf(typeof(RASCONN));
        var arr = new RASCONN[16];
        for (int i = 0; i < arr.Length; i++) arr[i].dwSize = size;
        int cb = size * arr.Length, n = 0;
        if (RasEnumConnections(arr, ref cb, ref n) == 0) for (int i = 0; i < n; i++) l.Add(arr[i].szEntryName);
        return l;
    }

    public static List<string> Entradas()
    {
        var list = new List<string>();
        string[] files = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Network\Connections\Pbk\rasphone.pbk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Network\Connections\Pbk\rasphone.pbk"),
        };
        foreach (var f in files)
        {
            try
            {
                if (!File.Exists(f)) continue;
                foreach (var raw in File.ReadAllLines(f))
                {
                    var l = raw.Trim();
                    if (l.Length > 2 && l[0] == '[' && l[l.Length - 1] == ']')
                    {
                        var name = l.Substring(1, l.Length - 2);
                        if (!list.Contains(name, StringComparer.OrdinalIgnoreCase)) list.Add(name);
                    }
                }
            }
            catch { }
        }
        return list;
    }

    public static int Rasdial(string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo("rasdial", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi))
        {
            p.StandardOutput.ReadToEndAsync(); p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return -1; }
            return p.ExitCode;
        }
    }
}
