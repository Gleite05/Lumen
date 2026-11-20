// Lumen Central — o fundo de ecrã passa a ser uma central de controlo.
//  * Fundo vivo: 8 camadas que se acendem de baixo para cima ao longo do horário de trabalho
//    (lumen.json). Quem olha vê só um fundo bonito; tu sabes quanto falta.
//  * Painéis no próprio fundo, por baixo dos ícones: servidores, agenda do Outlook, rede/VPN,
//    atalhos e música. O rato chega-lhes pelo espaço vazio do ambiente de trabalho (Rato.cs).
//  * Barra oculta: a barra de tarefas do Windows fica escondida (Ctrl+Alt+B mostra-a).
//  * Guarda: nos primeiros minutos garante que a barra do Lumen está a correr.
//  O que mudares no Painel (lumen.json) é aplicado na hora.
//  Lumen.Central.exe            arranca (uma instância)
//  Lumen.Central.exe --sair     fecha a que está a correr
//  Lumen.Central.exe --fundo P  só desenha o fundo vivo com o progresso P (0..1) em fundo-teste.png
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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class P
{
    public static Dictionary<string, object> Cfg = new Dictionary<string, object>();
    public static Tema Tema { get { return Temas.Obter(Lumen.S(Cfg, "tema")); } }

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);

    [STAThread]
    static void Main(string[] a)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        Directory.CreateDirectory(Lumen.Dados);
        Recarregar();
        if (a.Length > 0 && (a[0] == "--sair" || a[0] == "--parar")) { Sinal("Sair"); return; }
        if (a.Length > 1 && a[0] == "--fundo")
        {
            var b = Screen.PrimaryScreen.Bounds;
            using (var bmp = Fundo.Desenhar(b.Width, b.Height, double.Parse(a[1], CultureInfo.InvariantCulture)))
                bmp.Save(Path.Combine(Lumen.Dados, "fundo-teste.png"), ImageFormat.Png);
            return;
        }
        bool nova;
        using (var m = new Mutex(true, "Local\\Lumen.Central", out nova))
        {
            if (!nova) return;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            var j = new Janela();
            var sair = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Lumen.Central.Sair");
            new Thread(() => { sair.WaitOne(); try { j.BeginInvoke((Action)(() => j.Sair())); } catch { } }) { IsBackground = true }.Start();
            Application.Run(j);
        }
    }

    static void Sinal(string nome)
    {
        try { using (var e = EventWaitHandle.OpenExisting("Local\\Lumen.Central." + nome)) e.Set(); } catch { }
    }

    public static void Recarregar()
    {
        Cfg = Lumen.Ler();
        Lumen.DefinirIdioma(Cfg);
    }

    public static object C(string k) { return Lumen.C(Cfg, k); }
    public static IEnumerable<Dictionary<string, object>> Lista(string k) { return Lumen.Lista(Cfg, k); }
    public static string S(Dictionary<string, object> d, string k) { return Lumen.S(d, k); }
    public static bool Painel(string k)
    {
        var d = C("paineis") as Dictionary<string, object>;
        return Lumen.B(d, k, true);
    }
    public static bool AlgumPainel() { return new[] { "data", "servidores", "hoje", "rede", "atalhos", "musica" }.Any(Painel); }
    public static void Log(string s) { Lumen.Log("central", s); }
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
        if (blocos != null)
            foreach (var o in blocos)
            {
                var b = o as System.Collections.ArrayList;
                TimeSpan ti, tf;
                if (b == null || b.Count < 2 || !TimeSpan.TryParse(Convert.ToString(b[0]), out ti) || !TimeSpan.TryParse(Convert.ToString(b[1]), out tf) || tf <= ti) continue;
                var i = t.Date + ti; var f = t.Date + tf;
                double len = (f - i).TotalMinutes;
                total += len;
                feito += Math.Max(0, Math.Min(len, (t - i).TotalMinutes));
            }
        double p = total > 0 ? feito / total : 0;
        estado = p <= 0 ? "antes" : p >= 1 ? "depois" : "trabalho";
        return p;
    }

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
        var t = P.Tema;
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.ScaleTransform(W / 1920f, H / 1200f);
            using (var ceu = new LinearGradientBrush(new RectangleF(0, 0, 1920, 1200), Hex(t.Ceu1), Hex(t.Ceu2), 90f)) g.FillRectangle(ceu, -2, -2, 1924, 1204);
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
                        using (var b = new SolidBrush(Color.FromArgb(t.Claro ? 34 : 70, 0, 0, 0))) g.FillPath(b, sombra);
                    }
                    var c = Hex(t.Base[i]);
                    using (var b = new LinearGradientBrush(new RectangleF(0, y - 140, 1920, 420), Mais(c, 18), c, 90f)) g.FillPath(b, path);
                    if (v > 0)
                    {
                        int al = (int)(255 * (0.25 + 0.75 * v));
                        var luz = Hex(t.Luz[i]);
                        using (var b = new LinearGradientBrush(new RectangleF(0, y - 140, 1920, 420), Color.FromArgb(al, Mais(luz, 14)), Color.FromArgb(al, luz), 90f)) g.FillPath(b, path);
                    }
                }
                using (var crista = Banda(y, false))
                {
                    int a = v > 0 && v < 1 ? 150 : 26;
                    using (var pen = new Pen(Color.FromArgb(a, t.Crista[0], t.Crista[1], t.Crista[2]), v > 0 && v < 1 ? 2.2f : 1f)) g.DrawPath(pen, crista);
                }
            }
        }
        return bmp;
    }

    static Color Mais(Color c, int d) { return Color.FromArgb(c.A, Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d)); }

    static int ultimoPasso = -1;
    static string ultimoTema = "";
    static bool alterna;
    public static string Atual = "";   // o ficheiro do fundo em uso
    public static string Url = "";     // o mesmo, visto pela página
    public static Size Tamanho;        // tamanho da imagem

    // Põe o fundo conforme o modo (vivo | tema | meu). Devolve true se mudou.
    public static bool Atualizar(bool forcar)
    {
        string modo = Lumen.S(P.Cfg, "fundo");
        var b = Screen.PrimaryScreen.Bounds;
        if (modo == "tema")
        {
            string f = Path.Combine(Lumen.Pasta, "fundos", P.Tema.Id + ".png");
            if (!File.Exists(f)) return false;
            if (!forcar && f == Atual) return false;
            Aplicar(f);
            Url = "https://app.lumen.example/fundos/" + P.Tema.Id + ".png";
            Tamanho = TamanhoDe(f);
            return true;
        }
        if (modo == "meu")
        {
            string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            DateTime quando = File.Exists(f) ? File.GetLastWriteTime(f) : DateTime.MinValue;
            string chave = f + "|" + quando.Ticks;
            if (!forcar && chave == Atual) return false;
            Atual = chave;
            Url = File.Exists(f) ? "https://papel.lumen.example/TranscodedWallpaper?v=" + quando.Ticks : "";
            Tamanho = File.Exists(f) ? TamanhoDe(f) : b.Size;
            return true;
        }
        // vivo
        string estado;
        double p = Progresso(DateTime.Now, out estado);
        int passo = (int)Math.Round(p * 96);
        if (!forcar && passo == ultimoPasso && ultimoTema == P.Tema.Id && File.Exists(Atual)) return false;
        ultimoPasso = passo; ultimoTema = P.Tema.Id;
        string pasta = Path.Combine(Lumen.Dados, "fundo");
        Directory.CreateDirectory(pasta);
        alterna = !alterna;
        string nome = alterna ? "fundo-a.png" : "fundo-b.png";
        string fv = Path.Combine(pasta, nome);
        using (var bmp = Desenhar(b.Width, b.Height, passo / 96.0)) bmp.Save(fv, ImageFormat.Png);
        Aplicar(fv);
        Url = "https://dados.lumen.example/fundo/" + nome + "?v=" + DateTime.Now.Ticks;
        Tamanho = b.Size;
        return true;
    }

    static void Aplicar(string f)
    {
        try
        {
            var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            k.SetValue("WallpaperStyle", "10"); k.SetValue("TileWallpaper", "0"); k.Close();
        }
        catch { }
        SystemParametersInfo(0x14, 0, f, 3);
        Atual = f;
    }

    static Size TamanhoDe(string f)
    {
        try { using (var fs = File.OpenRead(f)) using (var img = Image.FromStream(fs, false, false)) return img.Size; }
        catch { return Screen.PrimaryScreen.Bounds.Size; }
    }
}

// ---------------------------------------------------------------- janela embutida no ambiente de trabalho
class Janela : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string c, string n);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr c, IntPtr p);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);

    readonly WebView2 web = new WebView2();
    readonly System.Windows.Forms.Timer relogio = new System.Windows.Forms.Timer { Interval = 30000 };
    readonly System.Windows.Forms.Timer adiar = new System.Windows.Forms.Timer { Interval = 500 };
    readonly Dados dados;
    readonly uint msgBarra = RegisterWindowMessage("TaskbarCreated");
    FileSystemWatcher vigia;
    BarraOculta barraOculta;
    Rectangle area;
    bool pronta, visivel;

    public Janela()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = ColorTranslator.FromHtml(P.Tema.Ceu2);
        Text = "Lumen";
        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = BackColor;
        Controls.Add(web);
        dados = new Dados(Enviar);
        dados.Avisar = (t, m) => Avisar(t, m);
        CriarIcone();
        Load += async (s, e) => await Iniciar();
        relogio.Tick += (s, e) => { if (Fundo.Atualizar(false)) EnviarFundo(); ManterPorBaixo(); };
        adiar.Tick += (s, e) => { adiar.Stop(); AplicarConfig(); };
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } // WS_EX_TOOLWINDOW: fora do Alt+Tab
    }

    // Começa escondida: só aparece se houver painéis ligados (Ancorar).
    protected override void SetVisibleCore(bool value) { base.SetVisibleCore(value && visivel); }

    Rectangle CalcularArea()
    {
        var b = Screen.PrimaryScreen.Bounds;
        var a = P.C("area") as Dictionary<string, object>;
        double x = 110, y = 40, w = 1400, h = 1070;
        try { if (a != null) { x = Convert.ToDouble(a["x"]); y = Convert.ToDouble(a["y"]); w = Convert.ToDouble(a["w"]); h = Convert.ToDouble(a["h"]); } } catch { }
        double kx = b.Width / 1920.0, ky = b.Height / 1200.0;
        return new Rectangle(b.X + (int)(x * kx), b.Y + (int)(y * ky), (int)(w * kx), (int)(h * ky));
    }

    void Ancorar()
    {
        visivel = P.AlgumPainel();
        if (!visivel)
        {
            Rato.Ligar(this, web, area, false);
            ShowWindow(Handle, 0);
            return;
        }
        area = CalcularArea();
        IntPtr prog = FindWindow("Progman", null);
        bool filho = prog != IntPtr.Zero && P.S(P.Cfg, "modo") != "fundo";
        if (filho)
        {
            int st = GetWindowLong(Handle, -16);
            st = (st & ~unchecked((int)0x80000000)) | 0x40000000; // tira WS_POPUP, põe WS_CHILD
            SetWindowLong(Handle, -16, st);
            // Por baixo dos ícones, como os fundos animados: o 0x052C pede ao Explorer uma camada própria
            // para o fundo (WorkerW, atrás da lista dos ícones) e a janela vai para dentro dela.
            IntPtr camada = CamadaDoFundo(prog);
            IntPtr pai = camada != IntPtr.Zero ? camada : prog;
            SetParent(Handle, pai);
            var pt = new Point(area.X, area.Y);
            ScreenToClient(pai, ref pt);
            SetWindowPos(Handle, IntPtr.Zero, pt.X, pt.Y, area.Width, area.Height, 0x0040 | 0x0010); // SHOWWINDOW | NOACTIVATE
            Rato.Ligar(this, web, area, camada != IntPtr.Zero);
        }
        else
        {
            Bounds = area;
            SetWindowPos(Handle, new IntPtr(1), area.X, area.Y, area.Width, area.Height, 0x0040 | 0x0010); // HWND_BOTTOM
        }
        P.Log("painéis em " + area + (filho ? " (no fundo, por baixo dos ícones)" : " (janela no fundo)"));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);

    static IntPtr CamadaDoFundo(IntPtr prog)
    {
        IntPtr r;
        SendMessageTimeout(prog, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 1000, out r);
        SendMessageTimeout(prog, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out r);
        // Windows 11 recente: o WorkerW é filho do Progman, atrás do SHELLDLL_DefView.
        IntPtr w = FindWindowEx(prog, IntPtr.Zero, "WorkerW", null);
        if (w != IntPtr.Zero) return w;
        // Antes: um WorkerW de topo logo a seguir à janela que tem o SHELLDLL_DefView.
        IntPtr achado = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                achado = FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
            return achado == IntPtr.Zero;
        }, IntPtr.Zero);
        return achado;
    }

    // Se o Explorer recriar a camada do fundo, a janela volta para dentro dela.
    void ManterPorBaixo()
    {
        if (!visivel || P.S(P.Cfg, "modo") == "fundo") return;
        IntPtr pai = GetParent(Handle);
        if (pai == IntPtr.Zero || !IsWindowClass(pai, "WorkerW")) Ancorar();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    static bool IsWindowClass(IntPtr h, string c) { var s = new System.Text.StringBuilder(64); GetClassName(h, s, 64); return s.ToString() == c; }

    async Task Iniciar()
    {
        Fundo.Atualizar(true);
        Ancorar();
        AplicarBarraOculta();
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Lumen.Dados, "webview"));
            await web.EnsureCoreWebView2Async(env);
            var c = web.CoreWebView2;
            c.Settings.AreDefaultContextMenusEnabled = false;
            c.Settings.IsZoomControlEnabled = false;
            c.Settings.IsStatusBarEnabled = false;
            c.SetVirtualHostNameToFolderMapping("app.lumen.example", Lumen.Pasta, CoreWebView2HostResourceAccessKind.Allow);
            c.SetVirtualHostNameToFolderMapping("dados.lumen.example", Lumen.Dados, CoreWebView2HostResourceAccessKind.Allow);
            string temas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes");
            if (Directory.Exists(temas)) c.SetVirtualHostNameToFolderMapping("papel.lumen.example", temas, CoreWebView2HostResourceAccessKind.Allow);
            c.WebMessageReceived += (o, a) => Mensagem(a.WebMessageAsJson);
            c.Navigate("https://app.lumen.example/web/central/index.html");
        }
        catch (Exception e) { P.Log("webview: " + e); }
        relogio.Start();
        dados.Iniciar();
        Guarda.Iniciar();
        try
        {
            vigia = new FileSystemWatcher(Lumen.Dados, "lumen.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            FileSystemEventHandler mudou = (s, e) => { try { BeginInvoke((Action)(() => { adiar.Stop(); adiar.Start(); })); } catch { } };
            vigia.Changed += mudou; vigia.Created += mudou;
            vigia.Renamed += (s, e) => mudou(s, null);
            vigia.EnableRaisingEvents = true;
        }
        catch (Exception e) { P.Log("vigia: " + e.Message); }
    }

    // O Painel gravou o lumen.json: tudo é aplicado sem reiniciar.
    void AplicarConfig()
    {
        P.Recarregar();
        P.Log("configuração recarregada");
        BackColor = ColorTranslator.FromHtml(P.Tema.Ceu2);
        Fundo.Atualizar(true);
        Ancorar();
        AplicarBarraOculta();
        MontarMenu();
        EnviarConfig();
        EnviarFundo();
        dados.Recarregar();
    }

    void AplicarBarraOculta()
    {
        bool quer = Lumen.B(P.Cfg, "barraOculta", true);
        if (quer && barraOculta == null) { barraOculta = new BarraOculta(); barraOculta.Mudou += MontarMenu; if (!barraOculta.AtalhoLivre) P.Log("barra oculta: Ctrl+Alt+B ocupado por outra app"); }
        else if (!quer && barraOculta != null) { barraOculta.Dispose(); barraOculta = null; }
    }

    NotifyIcon icone;
    void CriarIcone()
    {
        icone = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath), Text = "Lumen", Visible = Lumen.B(P.Cfg, "icone", true) };
        icone.DoubleClick += (s, e) => AbrirPainel();
        MontarMenu();
    }

    void MontarMenu()
    {
        if (icone == null) return;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(Lumen.T("Painel do Lumen…", "Lumen Panel…", "Panel de Lumen…"), null, (s, e) => AbrirPainel()) { Font = new Font(SystemFonts.MenuFont, FontStyle.Bold) });
        menu.Items.Add(Lumen.T("Definições da barra…", "Bar settings…", "Configuración de la barra…"), null, (s, e) => Abrir(Lumen.DefinicoesBarra, ""));
        if (barraOculta != null)
            menu.Items.Add(new ToolStripMenuItem(barraOculta.AMostrar ? Lumen.T("Esconder a barra do Windows", "Hide the Windows taskbar", "Ocultar la barra de Windows")
                : Lumen.T("Mostrar a barra do Windows", "Show the Windows taskbar", "Mostrar la barra de Windows"), null, (s, e) => barraOculta.Alternar()) { ShortcutKeyDisplayString = "Ctrl+Alt+B" });
        menu.Items.Add(Lumen.T("Modo Windows clássico", "Classic Windows mode", "Modo Windows clásico"), null, (s, e) => Abrir(Lumen.Classico, "original"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Lumen.T("Recarregar", "Reload", "Recargar"), null, (s, e) => { icone.Visible = false; if (barraOculta != null) barraOculta.Dispose(); Application.Restart(); });
        menu.Items.Add(Lumen.T("Sair", "Quit", "Salir"), null, (s, e) => Sair());
        var velho = icone.ContextMenuStrip;
        icone.ContextMenuStrip = menu;
        if (velho != null) velho.Dispose();
        icone.Text = "Lumen";
    }

    static void Abrir(string exe, string args)
    {
        try { if (File.Exists(exe)) Process.Start(exe, args); } catch (Exception e) { P.Log("abrir " + exe + ": " + e.Message); }
    }
    static void AbrirPainel() { Abrir(Lumen.Painel, ""); }

    public void Avisar(string titulo, string texto)
    {
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Avisar(titulo, texto))); } catch { } return; }
        if (icone == null) return;
        bool estava = icone.Visible;
        icone.Visible = true;
        icone.ShowBalloonTip(8000, titulo, texto, ToolTipIcon.Info);
        if (!estava)
        {
            var t = new System.Windows.Forms.Timer { Interval = 12000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); icone.Visible = Lumen.B(P.Cfg, "icone", true); };
            t.Start();
        }
    }

    void EnviarConfig()
    {
        var paineis = new Dictionary<string, object>();
        foreach (var k in new[] { "data", "servidores", "hoje", "rede", "atalhos", "musica" }) paineis[k] = P.Painel(k);
        Enviar(new Dictionary<string, object> { { "tipo", "config" }, { "idioma", Lumen.Lingua }, { "tema", P.Tema.ParaPagina() }, { "paineis", paineis } });
    }

    // A página pinta o mesmo fundo por trás dos painéis (como o "Preencher" do Windows), para ficar contínuo.
    void EnviarFundo()
    {
        var b = Screen.PrimaryScreen.Bounds;
        float k = DeviceDpi / 96f;
        double iw = Math.Max(1, Fundo.Tamanho.Width), ih = Math.Max(1, Fundo.Tamanho.Height);
        double esc = Math.Max(b.Width / iw, b.Height / ih);
        double dw = iw * esc, dh = ih * esc;
        double ox = (b.Width - dw) / 2, oy = (b.Height - dh) / 2;
        Enviar(new Dictionary<string, object> {
            { "tipo", "fundo" }, { "url", Fundo.Url },
            { "x", (area.X - b.X - ox) / k }, { "y", (area.Y - b.Y - oy) / k }, { "w", dw / k }, { "h", dh / k }
        });
    }

    void Mensagem(string json)
    {
        try
        {
            var m = Lumen.Json.Deserialize<Dictionary<string, object>>(json);
            string acao = P.S(m, "acao");
            if (acao == "pronto") { pronta = true; EnviarConfig(); EnviarFundo(); dados.EnviarTudo(); }
            else if (acao == "abrir") Acoes.Executar(P.S(m, "alvo"));
            else if (acao == "vpn") dados.Vpn(P.S(m, "nome"), P.S(m, "ligar") == "True");
            else if (acao == "media") dados.Media(P.S(m, "cmd"));
            else if (acao == "painel") AbrirPainel();
        }
        catch (Exception e) { P.Log("mensagem: " + e.Message); }
    }

    public void Enviar(object msg)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke((Action)(() => Enviar(msg))); } catch { } return; }
        var d = msg as Dictionary<string, object>;
        string tipo = d != null ? P.S(d, "tipo") : "";
        if (!pronta && tipo != "fundo" && tipo != "config") return;
        try { if (web.CoreWebView2 != null) web.CoreWebView2.PostWebMessageAsJson(Lumen.Json.Serialize(msg)); } catch { }
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

    public void Sair()
    {
        dados.Parar();
        if (barraOculta != null) { barraOculta.Dispose(); barraOculta = null; }
        if (icone != null) icone.Visible = false;
        Close();
    }
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
