// Lumen Painel — onde se personaliza tudo: tema, fundo, cursores, painéis do ambiente de trabalho,
// horário, servidores, atalhos, notificações, idioma, arranque e atualizações.
// Grava no lumen.json (o Central aplica na hora); as opções da barra vão para o settings.json dela.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class Programa
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);

    [STAThread]
    static void Main(string[] a)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        bool nova;
        using (var m = new Mutex(true, "Local\\Lumen.Painel", out nova))
        {
            if (!nova)
            {
                // já está aberto: traz para a frente
                var h = FindWindow(null, "Lumen");
                foreach (var p in Process.GetProcessesByName("Lumen.Painel")) if (p.MainWindowHandle != IntPtr.Zero) h = p.MainWindowHandle;
                if (h != IntPtr.Zero) { ShowWindow(h, 9); SetForegroundWindow(h); }
                return;
            }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.Run(new Painel(a.Length > 0 ? a[0] : ""));
        }
    }
}

class Painel : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    readonly WebView2 web = new WebView2();
    readonly string pagina;
    Dictionary<string, object> cfg;

    public Painel(string pagina)
    {
        this.pagina = pagina;
        cfg = Lumen.Ler();
        Lumen.DefinirIdioma(cfg);
        var t = Temas.Obter(Lumen.S(cfg, "tema"));
        Text = "Lumen";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        var wa = Screen.PrimaryScreen.WorkingArea;
        float k = DeviceDpi / 96f;
        Size = new Size(Math.Min((int)(1180 * k), wa.Width - 40), Math.Min((int)(800 * k), wa.Height - 40));
        MinimumSize = new Size((int)(860 * k), (int)(600 * k));
        BackColor = ColorTranslator.FromHtml(t.Ceu2);
        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = BackColor;
        Controls.Add(web);
        Load += async (s, e) => await Iniciar();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TituloEscuro();
    }

    // Barra de título escura, da cor do tema (Windows 11).
    void TituloEscuro()
    {
        try
        {
            var t = Temas.Obter(Lumen.S(cfg, "tema"));
            int um = t.Claro ? 0 : 1; DwmSetWindowAttribute(Handle, 20, ref um, 4);
            var c = ColorTranslator.FromHtml(t.Ceu2); int cor = c.R | (c.G << 8) | (c.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref cor, 4);
            int canto = 2; DwmSetWindowAttribute(Handle, 33, ref canto, 4);
        }
        catch { }
    }

    async Task Iniciar()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Lumen.Dados, "webview-painel"));
            await web.EnsureCoreWebView2Async(env);
            var c = web.CoreWebView2;
            c.Settings.AreDefaultContextMenusEnabled = false;
            c.Settings.IsZoomControlEnabled = false;
            c.Settings.IsStatusBarEnabled = false;
            c.Settings.AreDevToolsEnabled = false;
            c.SetVirtualHostNameToFolderMapping("app.lumen.example", Lumen.Pasta, CoreWebView2HostResourceAccessKind.Allow);
            c.WebMessageReceived += (o, a) => Mensagem(a.WebMessageAsJson);
            c.NewWindowRequested += (o, a) => { a.Handled = true; Abrir(a.Uri); };
            c.Navigate("https://app.lumen.example/web/painel/index.html" + (pagina != "" ? "#" + pagina : ""));
        }
        catch (Exception e)
        {
            Lumen.Log("painel", "webview: " + e);
            MessageBox.Show(Lumen.T("O Painel precisa do Microsoft Edge WebView2 Runtime.", "The Panel needs the Microsoft Edge WebView2 Runtime.", "El Panel necesita Microsoft Edge WebView2 Runtime.") + "\n\n" + e.Message, "Lumen");
        }
    }

    void Enviar(string tipo, object dados)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Enviar(tipo, dados))); } catch { } return; }
        try { web.CoreWebView2.PostWebMessageAsJson(Lumen.Json.Serialize(new Dictionary<string, object> { { "tipo", tipo }, { "dados", dados } })); } catch { }
    }

    void EnviarEstado()
    {
        var barra = Aplicar.LerBarra();
        Enviar("estado", new Dictionary<string, object> {
            { "cfg", cfg },
            { "lingua", Lumen.Lingua },
            { "temas", Temas.Todos.Select(t => (object)t.ParaPagina()).ToArray() },
            { "versao", Lumen.Versao },
            { "barra", new Dictionary<string, object> {
                { "hideBanners", Lumen.B(barra, "hideBanners", false) }, { "remoteVisible", Lumen.B(barra, "remoteVisible", false) },
                { "language", Lumen.S(barra, "language") } } },
            { "arranque", Aplicar.ArranqueLigado() },
            { "aCorrer", new Dictionary<string, object> {
                { "barra", Process.GetProcessesByName("Lumen").Length > 0 }, { "central", Process.GetProcessesByName("Lumen.Central").Length > 0 } } },
            { "temOutlook", Type.GetTypeFromProgID("Outlook.Application") != null },
        });
    }

    void Mensagem(string json)
    {
        try
        {
            var m = Lumen.Json.Deserialize<Dictionary<string, object>>(json);
            string acao = Lumen.S(m, "acao");
            switch (acao)
            {
                case "pronto": EnviarEstado(); break;
                case "gravar": Gravar(m["cfg"] as Dictionary<string, object>); break;
                case "barra":
                    {
                        string chave = Lumen.S(m, "chave"); object valor = m["valor"];
                        Enviar("ocupado", true);
                        new Thread(() =>
                        {
                            try { Aplicar.AlterarBarra(new Dictionary<string, object> { { chave, valor } }); }
                            catch (Exception e) { Lumen.Log("painel", "barra " + chave + ": " + e.Message); }
                            Enviar("ocupado", false); EnviarEstadoSeguro();
                        }) { IsBackground = true }.Start();
                        break;
                    }
                case "arranque": Aplicar.Arranque(Lumen.S(m, "ligado") == "True"); EnviarEstado(); break;
                case "abrir": Abrir(Lumen.S(m, "alvo")); break;
                case "reiniciar":
                    Enviar("ocupado", true);
                    new Thread(() => { Aplicar.PararTudo(); Aplicar.IniciarTudo(); Thread.Sleep(1500); Enviar("ocupado", false); EnviarEstadoSeguro(); }) { IsBackground = true }.Start();
                    break;
                case "testar":
                    {
                        string id = Lumen.S(m, "id"), host = Lumen.S(m, "host"), porta = Lumen.S(m, "porta");
                        Task.Run(() => { long ms; bool ok = Testar(host, porta, out ms); Enviar("teste", new Dictionary<string, object> { { "id", id }, { "ok", ok }, { "ms", ms } }); });
                        break;
                    }
                case "atualizacoes": Task.Run(() => ProcurarAtualizacao()); break;
                case "atualizar": { string url = Lumen.S(m, "url"); Task.Run(() => Atualizar(url)); break; }
            }
        }
        catch (Exception e) { Lumen.Log("painel", "mensagem: " + e); }
    }

    void EnviarEstadoSeguro() { try { BeginInvoke((Action)EnviarEstado); } catch { } }

    void Gravar(Dictionary<string, object> novo)
    {
        if (novo == null) return;
        string cursoresAntes = Lumen.S(cfg, "cursores"), idiomaAntes = Lumen.S(cfg, "idioma"), temaAntes = Lumen.S(cfg, "tema");
        foreach (var kv in novo) cfg[kv.Key] = kv.Value;
        Lumen.Gravar(cfg); // o Central vê a mudança e aplica-a
        Lumen.DefinirIdioma(cfg);
        if (Lumen.S(cfg, "cursores") != cursoresAntes) Aplicar.Cursores(Lumen.S(cfg, "cursores"));
        if (Lumen.S(cfg, "tema") != temaAntes) { BackColor = ColorTranslator.FromHtml(Temas.Obter(Lumen.S(cfg, "tema")).Ceu2); TituloEscuro(); }
        if (Lumen.S(cfg, "idioma") != idiomaAntes)
        {
            string tag = Lumen.TagBarra;
            new Thread(() => { try { Aplicar.AlterarBarra(new Dictionary<string, object> { { "language", tag } }); } catch { } }) { IsBackground = true }.Start();
            EnviarEstado();
        }
        Enviar("gravado", true);
    }

    static void Abrir(string alvo)
    {
        try
        {
            if (alvo == "definicoesBarra") Process.Start(Lumen.DefinicoesBarra);
            else if (alvo == "classico") Process.Start(Lumen.Classico, "original");
            else if (alvo == "pasta") Process.Start("explorer.exe", Lumen.Dados);
            else if (alvo == "licencas") Process.Start("notepad.exe", Path.Combine(Lumen.Pasta, "THIRD-PARTY-NOTICES.txt"));
            else if (alvo.StartsWith("https://")) Process.Start(new ProcessStartInfo(alvo) { UseShellExecute = true });
        }
        catch (Exception e) { Lumen.Log("painel", "abrir " + alvo + ": " + e.Message); }
    }

    static bool Testar(string host, string porta, out long ms)
    {
        ms = -1;
        if (string.IsNullOrEmpty(host)) return false;
        try { using (var p = new Ping()) { var r = p.Send(host, 1500); if (r.Status == IPStatus.Success) { ms = r.RoundtripTime; return true; } } } catch { }
        int po;
        if (int.TryParse(porta, out po) && po > 0)
        {
            var sw = Stopwatch.StartNew();
            try { using (var c = new TcpClient()) { var t = c.ConnectAsync(host, po); if (t.Wait(2000) && c.Connected) { ms = sw.ElapsedMilliseconds; return true; } } } catch { }
        }
        return false;
    }

    // ------------------------------------------------------------ atualizações (GitHub Releases)
    void ProcurarAtualizacao()
    {
        try
        {
            string json;
            using (var w = new WebClient())
            {
                w.Headers.Add("User-Agent", "Lumen/" + Lumen.Versao);
                w.Headers.Add("Accept", "application/vnd.github+json");
                json = w.DownloadString("https://api.github.com/repos/" + Lumen.Repo + "/releases/latest");
            }
            var r = Lumen.Json.Deserialize<Dictionary<string, object>>(json);
            string tag = Lumen.S(r, "tag_name").TrimStart('v', 'V');
            string url = "";
            var assets = r.ContainsKey("assets") ? r["assets"] as System.Collections.ArrayList : null;
            if (assets != null)
                foreach (Dictionary<string, object> a in assets)
                    if (Lumen.S(a, "name").EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && Lumen.S(a, "name").IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0) url = Lumen.S(a, "browser_download_url");
            Version nova, atual;
            bool ha = Version.TryParse(tag, out nova) && Version.TryParse(Lumen.Versao, out atual) && nova > atual && url != "";
            Enviar("atualizacao", new Dictionary<string, object> { { "ok", true }, { "disponivel", ha }, { "versao", tag }, { "url", url }, { "notas", Lumen.S(r, "body") } });
        }
        catch (Exception e) { Enviar("atualizacao", new Dictionary<string, object> { { "ok", false }, { "erro", e.Message } }); }
    }

    void Atualizar(string url)
    {
        try
        {
            if (!url.StartsWith("https://github.com/" + Lumen.Repo + "/releases/download/", StringComparison.OrdinalIgnoreCase)) throw new Exception("endereço inesperado");
            string destino = Path.Combine(Path.GetTempPath(), "Lumen-Setup.exe");
            using (var w = new WebClient())
            {
                w.Headers.Add("User-Agent", "Lumen/" + Lumen.Versao);
                w.DownloadProgressChanged += (s, e) => Enviar("progresso", e.ProgressPercentage);
                w.DownloadFileTaskAsync(new Uri(url), destino).Wait();
            }
            Process.Start(destino, "/SILENT /SUPPRESSMSGBOXES /NORESTART /LUMENATUALIZAR");
            BeginInvoke((Action)Close);
        }
        catch (Exception e) { Enviar("atualizacao", new Dictionary<string, object> { { "ok", false }, { "erro", e.InnerException != null ? e.InnerException.Message : e.Message } }); }
    }
}
