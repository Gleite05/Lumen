// Central — o fundo de ecrã passa a ser uma central de controlo.
//  * O fundo (imagem do Windows) é desenhado aqui: 8 camadas que se acendem de baixo para cima
//    com as horas de trabalho do horário definido em config.json. Só quem sabe percebe.
//  * Por cima, uma janela embutida no ambiente de trabalho (filha do Progman, como os ícones)
//    mostra os painéis: servidores, agenda do Outlook, rede/VPN, atalhos e música.
//  Central.exe            arranca (uma instância)
//  Central.exe --sair     fecha a que está a correr
//  Central.exe --fundo P  só desenha o fundo com o progresso P (0..1) em fundo-teste.png
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class P
{
    public static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".central");
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    public static Dictionary<string, object> Cfg = new Dictionary<string, object>();

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);

    [STAThread]
    static void Main(string[] a)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        Directory.CreateDirectory(Pasta);
        LerConfig();
        if (a.Length > 0 && a[0] == "--sair") { Sinal("Sair"); return; }
        if (a.Length > 1 && a[0] == "--fundo")
        {
            var b = Screen.PrimaryScreen.Bounds;
            using (var bmp = Fundo.Desenhar(b.Width, b.Height, double.Parse(a[1], CultureInfo.InvariantCulture)))
                bmp.Save(Path.Combine(Pasta, "fundo-teste.png"), ImageFormat.Png);
            return;
        }
        bool nova;
        using (var m = new Mutex(true, "Local\\Central.Gleite05", out nova))
        {
            if (!nova) return;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            var j = new Janela();
            var sair = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Central.Sair");
            new Thread(() => { sair.WaitOne(); try { j.BeginInvoke((Action)(() => j.Sair())); } catch { } }) { IsBackground = true }.Start();
            Application.Run(j);
        }
    }

    static void Sinal(string nome)
    {
        try { using (var e = EventWaitHandle.OpenExisting("Local\\Central." + nome)) e.Set(); } catch { }
    }

    public static void LerConfig()
    {
        try
        {
            var f = Path.Combine(Pasta, "config.json");
            if (File.Exists(f)) Cfg = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(f));
        }
        catch (Exception e) { Log("config: " + e.Message); }
    }

    public static object C(string k) { object v; return Cfg.TryGetValue(k, out v) ? v : null; }
    public static IEnumerable<Dictionary<string, object>> Lista(string k)
    {
        var l = C(k) as System.Collections.ArrayList;
        if (l == null) yield break;
        foreach (var o in l) { var d = o as Dictionary<string, object>; if (d != null) yield return d; }
    }
    public static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }

    static readonly object logLock = new object();
    public static void Log(string s)
    {
        lock (logLock) try { File.AppendAllText(Path.Combine(Pasta, "central.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + s + "\r\n"); } catch { }
    }
}

// ---------------------------------------------------------------- horário e fundo
static class Fundo
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);

    public static double Progresso(DateTime t, out string estado)
    {
        var dias = new HashSet<int>();
        var ld = P.C("dias") as System.Collections.ArrayList;
        if (ld != null) foreach (var o in ld) dias.Add(Convert.ToInt32(o)); else for (int i = 1; i <= 5; i++) dias.Add(i);
        var fer = new HashSet<string>();
        var lf = P.C("feriados") as System.Collections.ArrayList;
        if (lf != null) foreach (var o in lf) fer.Add(Convert.ToString(o));
        if (!dias.Contains((int)t.DayOfWeek) || fer.Contains(t.ToString("yyyy-MM-dd"))) { estado = "livre"; return 0; }

        double total = 0, feito = 0;
        var blocos = P.C("horario") as System.Collections.ArrayList;
        if (blocos == null) blocos = new System.Collections.ArrayList { new System.Collections.ArrayList { "09:00", "14:00" }, new System.Collections.ArrayList { "15:00", "18:00" } };
        foreach (System.Collections.ArrayList b in blocos)
        {
            var i = t.Date + TimeSpan.Parse(Convert.ToString(b[0]));
            var f = t.Date + TimeSpan.Parse(Convert.ToString(b[1]));
            double len = (f - i).TotalMinutes;
            total += len;
            feito += Math.Max(0, Math.Min(len, (t - i).TotalMinutes));
        }
        double p = total > 0 ? feito / total : 0;
        estado = p <= 0 ? "antes" : p >= 1 ? "depois" : "trabalho";
        return p;
    }

    static readonly Color[] Base = new[] { "#1d5236", "#1a4c32", "#17462e", "#15402a", "#133b27", "#103322", "#0d2a1c", "#0a2116" }.Select(Hex).ToArray();
    static readonly Color[] Luz = new[] { "#2f8a58", "#38935f", "#429c67", "#4ea670", "#5bb07a", "#6aba85", "#7bc592", "#8fd0a1" }.Select(Hex).ToArray();
    static Color Hex(string s) { return ColorTranslator.FromHtml(s); }

    static GraphicsPath Banda(float y, bool fechada)
    {
        var gp = new GraphicsPath();
        gp.AddBezier(0, y + 60, 420, y - 50, 900, y + 150, 1400, y + 10);
        gp.AddBezier(1400, y + 10, 1900, y - 130, 1920, y - 40, 1920, y - 40);
        if (fechada) { gp.AddLine(1920, y - 40, 1920, 1210); gp.AddLine(1920, 1210, 0, 1210); gp.CloseFigure(); }
        return gp;
    }

    public static Bitmap Desenhar(int W, int H, double p)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.ScaleTransform(W / 1920f, H / 1200f);
            using (var ceu = new LinearGradientBrush(new RectangleF(0, 0, 1920, 1200), Hex("#08140e"), Hex("#04100a"), 90f)) g.FillRectangle(ceu, -2, -2, 1924, 1204);
            float L = (float)(p * 8);
            for (int i = 7; i >= 0; i--)
            {
                float y = 1200 - (i + 1) * 125 + 20;
                float v = Math.Max(0, Math.Min(1, L - i));
                using (var path = Banda(y, true))
                {
                    using (var sombra = (GraphicsPath)path.Clone())
                    {
                        using (var m = new Matrix()) { m.Translate(0, -12); sombra.Transform(m); }
                        using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, sombra);
                    }
                    var c = Base[i];
                    using (var b = new LinearGradientBrush(new RectangleF(0, y - 140, 1920, 420), Mais(c, 18), c, 90f)) g.FillPath(b, path);
                    if (v > 0)
                    {
                        int al = (int)(255 * (0.25 + 0.75 * v));
                        using (var b = new LinearGradientBrush(new RectangleF(0, y - 140, 1920, 420), Color.FromArgb(al, Mais(Luz[i], 14)), Color.FromArgb(al, Luz[i]), 90f)) g.FillPath(b, path);
                    }
                }
                using (var crista = Banda(y, false))
                {
                    int a = v > 0 && v < 1 ? 150 : 26;
                    using (var pen = new Pen(Color.FromArgb(a, 201, 255, 220), v > 0 && v < 1 ? 2.2f : 1f)) g.DrawPath(pen, crista);
                }
            }
        }
        return bmp;
    }

    static Color Mais(Color c, int d) { return Color.FromArgb(c.A, Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d)); }

    static int ultimoPasso = -1;
    static bool alterna;
    public static string Atual = "";

    // Redesenha só quando o progresso muda de "passo" (5 min); devolve true se mudou.
    public static bool Atualizar(bool forcar)
    {
        string estado;
        double p = Progresso(DateTime.Now, out estado);
        int passo = (int)Math.Round(p * 96);
        if (!forcar && passo == ultimoPasso && File.Exists(Atual)) return false;
        ultimoPasso = passo;
        var b = Screen.PrimaryScreen.Bounds;
        alterna = !alterna;
        var f = Path.Combine(P.Pasta, alterna ? "fundo-a.png" : "fundo-b.png");
        using (var bmp = Desenhar(b.Width, b.Height, passo / 96.0)) bmp.Save(f, ImageFormat.Png);
        try
        {
            var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            k.SetValue("WallpaperStyle", "10"); k.SetValue("TileWallpaper", "0"); k.Close();
        }
        catch { }
        SystemParametersInfo(0x14, 0, f, 3);
        Atual = f;
        return true;
    }
}

// ---------------------------------------------------------------- janela embutida no ambiente de trabalho
class Janela : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr c, IntPtr p);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);

    readonly WebView2 web = new WebView2();
    readonly System.Windows.Forms.Timer relogio = new System.Windows.Forms.Timer { Interval = 30000 };
    readonly Dados dados;
    readonly uint msgBarra = RegisterWindowMessage("TaskbarCreated");
    Rectangle area;
    bool pronta;

    public Janela()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(6, 23, 15);
        Text = "Central";
        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = Color.FromArgb(6, 23, 15);
        Controls.Add(web);
        dados = new Dados(Enviar);
        dados.Avisar = (t, m) => Avisar(t, m);
        CriarIcone();
        Load += async (s, e) => await Iniciar();
        relogio.Tick += (s, e) => { if (Fundo.Atualizar(false)) EnviarFundo(); };
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } // WS_EX_TOOLWINDOW: fora do Alt+Tab
    }

    Rectangle CalcularArea()
    {
        var b = Screen.PrimaryScreen.Bounds;
        var a = P.C("area") as Dictionary<string, object>;
        double x = 110, y = 40, w = 1400, h = 1070;
        if (a != null) { x = Convert.ToDouble(a["x"]); y = Convert.ToDouble(a["y"]); w = Convert.ToDouble(a["w"]); h = Convert.ToDouble(a["h"]); }
        double kx = b.Width / 1920.0, ky = b.Height / 1200.0;
        return new Rectangle(b.X + (int)(x * kx), b.Y + (int)(y * ky), (int)(w * kx), (int)(h * ky));
    }

    void Ancorar()
    {
        area = CalcularArea();
        IntPtr prog = FindWindow("Progman", null);
        bool filho = prog != IntPtr.Zero && P.S(P.Cfg, "modo") != "fundo";
        if (filho)
        {
            int st = GetWindowLong(Handle, -16);
            st = (st & ~unchecked((int)0x80000000)) | 0x40000000; // tira WS_POPUP, põe WS_CHILD
            SetWindowLong(Handle, -16, st);
            SetParent(Handle, prog);
            var pt = new Point(area.X, area.Y);
            ScreenToClient(prog, ref pt);
            SetWindowPos(Handle, IntPtr.Zero, pt.X, pt.Y, area.Width, area.Height, 0x0040 | 0x0010); // SHOWWINDOW | NOACTIVATE, topo dos filhos
        }
        else
        {
            Bounds = area;
            SetWindowPos(Handle, new IntPtr(1), area.X, area.Y, area.Width, area.Height, 0x0040 | 0x0010); // HWND_BOTTOM
        }
        P.Log("ancorada " + (filho ? "no Progman" : "no fundo") + " em " + area);
    }

    async Task Iniciar()
    {
        Fundo.Atualizar(true);
        Ancorar();
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(P.Pasta, "webview-dados"));
            await web.EnsureCoreWebView2Async(env);
            var c = web.CoreWebView2;
            c.Settings.AreDefaultContextMenusEnabled = false;
            c.Settings.IsZoomControlEnabled = false;
            c.Settings.IsStatusBarEnabled = false;
            c.SetVirtualHostNameToFolderMapping("central.local", P.Pasta, CoreWebView2HostResourceAccessKind.Allow);
            c.WebMessageReceived += (o, a) => Mensagem(a.WebMessageAsJson);
            c.Navigate("https://central.local/web/index.html");
        }
        catch (Exception e) { P.Log("webview: " + e); }
        relogio.Start();
        dados.Iniciar();
        Guarda.Avisar = Avisar;
        Guarda.Iniciar();
    }

    NotifyIcon icone;
    void CriarIcone()
    {
        icone = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath), Text = "Central", Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Recarregar", null, (s, e) => { icone.Visible = false; Application.Restart(); });
        menu.Items.Add("Abrir a pasta (config.json)", null, (s, e) => Process.Start("explorer.exe", P.Pasta));
        menu.Items.Add("Sair", null, (s, e) => Sair());
        icone.ContextMenuStrip = menu;
    }

    public void Avisar(string titulo, string texto)
    {
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Avisar(titulo, texto))); } catch { } return; }
        if (icone != null) icone.ShowBalloonTip(8000, titulo, texto, ToolTipIcon.Info);
    }

    void EnviarFundo()
    {
        var b = Screen.PrimaryScreen.Bounds;
        float k = DeviceDpi / 96f;
        Enviar(new Dictionary<string, object> {
            { "tipo", "fundo" },
            { "url", "https://central.local/" + Path.GetFileName(Fundo.Atual) + "?v=" + DateTime.Now.Ticks },
            { "x", (area.X - b.X) / k }, { "y", (area.Y - b.Y) / k }, { "w", b.Width / k }, { "h", b.Height / k }
        });
    }

    void Mensagem(string json)
    {
        try
        {
            var m = P.Json.Deserialize<Dictionary<string, object>>(json);
            string acao = P.S(m, "acao");
            if (acao == "pronto") { pronta = true; EnviarFundo(); dados.EnviarTudo(); }
            else if (acao == "abrir") Acoes.Executar(P.S(m, "alvo"));
            else if (acao == "vpn") dados.Vpn(P.S(m, "nome"), P.S(m, "ligar") == "True");
            else if (acao == "media") dados.Media(P.S(m, "cmd"));
        }
        catch (Exception e) { P.Log("mensagem: " + e.Message); }
    }

    public void Enviar(object msg)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Enviar(msg))); } catch { } return; }
        if (!pronta && !(msg is Dictionary<string, object> && P.S((Dictionary<string, object>)msg, "tipo") == "fundo")) return;
        try { web.CoreWebView2.PostWebMessageAsJson(P.Json.Serialize(msg)); } catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == msgBarra && msgBarra != 0) // o Explorer reiniciou: o Progman é novo
        {
            var t = new System.Windows.Forms.Timer { Interval = 3000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); Ancorar(); };
            t.Start();
        }
        else if (m.Msg == 0x007E) // WM_DISPLAYCHANGE
        {
            Fundo.Atualizar(true); Ancorar(); EnviarFundo();
        }
        base.WndProc(ref m);
    }

    public void Sair() { dados.Parar(); if (icone != null) icone.Visible = false; Close(); }
}

// ---------------------------------------------------------------- ações dos botões
static class Acoes
{
    // rdp:host · ssh:user@host · url:https://… · exe:caminho|args
    public static void Executar(string alvo)
    {
        try
        {
            if (string.IsNullOrEmpty(alvo)) return;
            int i = alvo.IndexOf(':');
            string tipo = i > 0 ? alvo.Substring(0, i) : "url", resto = i > 0 ? alvo.Substring(i + 1) : alvo;
            if (tipo == "rdp") Process.Start("mstsc.exe", "/v:" + resto);
            else if (tipo == "ssh")
            {
                string putty = new[] { @"C:\Program Files\PuTTY\putty.exe", @"C:\Program Files (x86)\PuTTY\putty.exe" }.FirstOrDefault(File.Exists);
                if (putty != null) Process.Start(putty, "-ssh " + resto);
                else Process.Start("cmd.exe", "/k ssh " + resto);
            }
            else if (tipo == "exe") { var p = resto.Split('|'); Process.Start(p[0], p.Length > 1 ? p[1] : ""); }
            else Process.Start(new ProcessStartInfo(tipo == "url" ? resto : alvo) { UseShellExecute = true });
        }
        catch (Exception e) { P.Log("ação " + alvo + ": " + e.Message); }
    }
}
