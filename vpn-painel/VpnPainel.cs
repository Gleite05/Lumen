// VpnPainel — ícone VPN na área de notificação (aparece na barra do Atrium) com um painel
// para ligar/desligar as VPNs do Windows (as do rasphone.pbk do utilizador e de todos).
// Ligar = rasdial "<nome>" (credenciais guardadas); se faltarem, abre o rasphone -d.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [STAThread]
    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "Local\\VpnPainel.Gleite05", out created))
        {
            if (!created)
            {
                // já está a correr: pede à instância aberta para mostrar o painel (serve para um atalho/pin)
                EventWaitHandle ev;
                foreach (var p in Process.GetProcessesByName("VpnPainel"))
                    if (p.Id != Process.GetCurrentProcess().Id) AllowSetForegroundWindow(p.Id); // deixa o painel ficar à frente
                if (EventWaitHandle.TryOpenExisting("Local\\VpnPainel.Abrir", out ev)) { ev.Set(); ev.Dispose(); }
                return;
            }
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.Run(new VpnCtx());
        }
    }
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

    public static HashSet<string> Active()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int size = Marshal.SizeOf(typeof(RASCONN));
        var arr = new RASCONN[16];
        for (int i = 0; i < arr.Length; i++) arr[i].dwSize = size;
        int cb = size * arr.Length, n = 0;
        int r = RasEnumConnections(arr, ref cb, ref n);
        if (r == 0) for (int i = 0; i < n; i++) set.Add(arr[i].szEntryName);
        else if (r == 632) return RasdialFallback(); // tamanho de estrutura não aceite: lê pelo rasdial
        return set;
    }

    static HashSet<string> RasdialFallback()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = Entries();
        try
        {
            var psi = new ProcessStartInfo("rasdial") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi))
            {
                foreach (var line in p.StandardOutput.ReadToEnd().Split('\n'))
                {
                    var l = line.Trim();
                    foreach (var e in names) if (string.Equals(e, l, StringComparison.OrdinalIgnoreCase)) set.Add(e);
                }
                p.WaitForExit(3000);
            }
        }
        catch { }
        return set;
    }

    public static List<string> Entries()
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

class VpnCtx : ApplicationContext
{
    readonly NotifyIcon icon = new NotifyIcon();
    readonly Painel painel;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    public List<string> Entries = new List<string>();
    public HashSet<string> Active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, string> Busy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // a ligar / a desligar
    public readonly Dictionary<string, string> Errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string iconKey = "";
    DateTime lastEntries = DateTime.MinValue;
    int promoteTries;

    public VpnCtx()
    {
        painel = new Painel(this);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir o painel da VPN", null, (s, e) => painel.ShowNear(Cursor.Position));
        menu.Items.Add("Definições de VPN do Windows…", null, (s, e) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (s, e) => ExitThread());
        icon.ContextMenuStrip = menu;
        icon.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) painel.Toggle(Cursor.Position); };
        Refresh(true);
        icon.Visible = true;

        timer.Interval = 2000;
        timer.Tick += (s, e) => Refresh(false);
        timer.Start();

        var ui = SynchronizationContext.Current;
        var abrir = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VpnPainel.Abrir");
        new Thread(() => { while (abrir.WaitOne()) ui.Post(_ => painel.Toggle(Cursor.Position, 700), null); }) { IsBackground = true }.Start();
    }

    public static void OpenSettings() { try { Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true }); } catch { } }

    public void Refresh(bool force)
    {
        if (force || (DateTime.Now - lastEntries).TotalSeconds > 30) { Entries = Ras.Entries(); lastEntries = DateTime.Now; }
        var a = Ras.Active();
        bool changed = !a.SetEquals(Active);
        Active = a;
        UpdateIcon();
        if (changed || force) painel.Redraw();
        if (promoteTries < 6) { promoteTries++; Promote(); }
    }

    // Garante que o ícone fica à vista na barra (e não escondido atrás do ^).
    static void Promote()
    {
        try
        {
            string me = Application.ExecutablePath;
            using (var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
            {
                if (root == null) return;
                foreach (var name in root.GetSubKeyNames())
                    using (var k = root.OpenSubKey(name, true))
                    {
                        var path = k == null ? null : k.GetValue("ExecutablePath") as string;
                        if (path != null && string.Equals(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)), me, StringComparison.OrdinalIgnoreCase)
                            && !(k.GetValue("IsPromoted") is int && (int)k.GetValue("IsPromoted") == 1))
                            k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                    }
            }
        }
        catch { }
    }

    void UpdateIcon()
    {
        bool busy = Busy.Count > 0, on = Active.Count > 0;
        string key = (on ? "on" : "off") + (busy ? "b" : "");
        string tip = on ? "VPN · " + string.Join(", ", Active) + (Active.Count == 1 ? " ligada" : " ligadas") : (busy ? "VPN · a ligar…" : "VPN · nenhuma ligada");
        if (tip.Length > 63) tip = tip.Substring(0, 62) + "…";
        icon.Text = tip;
        if (key == iconKey) return;
        iconKey = key;
        var old = icon.Icon;
        icon.Icon = MakeIcon(on, busy);
        if (old != null) old.Dispose();
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static Icon MakeIcon(bool on, bool busy)
    {
        using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            Color fill = on ? Color.FromArgb(255, 34, 165, 91) : busy ? Color.FromArgb(255, 217, 145, 30) : Color.FromArgb(255, 96, 104, 120);
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, 1, 1, 30, 30);
            using (var p = new Pen(Color.FromArgb(70, 0, 0, 0), 1f)) g.DrawEllipse(p, 1, 1, 30, 30);
            using (var f = new Font(Painel.IconFont, 15f, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("", f, Brushes.White, new RectangleF(0, 1, 32, 32), sf);
            IntPtr h = bmp.GetHicon();
            var ic = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return ic;
        }
    }

    public void ToggleVpn(string name)
    {
        if (Busy.ContainsKey(name)) return;
        bool connect = !Active.Contains(name);
        Busy[name] = connect ? "A ligar…" : "A desligar…";
        Errors.Remove(name);
        UpdateIcon(); painel.Redraw();
        var ui = SynchronizationContext.Current;
        new Thread(() =>
        {
            int code = connect ? Ras.Rasdial("\"" + name + "\"", 90000) : Ras.Rasdial("\"" + name + "\" /disconnect", 20000);
            ui.Post(_ => Done(name, connect, code), null);
        }) { IsBackground = true }.Start();
    }

    void Done(string name, bool connect, int code)
    {
        Busy.Remove(name);
        if (connect && code != 0)
        {
            if (code == 691 || code == 703 || code == 718)
            {
                Errors[name] = code == 691 ? "Credenciais recusadas — introduz de novo" : "Introduz as credenciais na janela";
                try { Process.Start(new ProcessStartInfo("rasphone", "-d \"" + name + "\"") { UseShellExecute = true }); } catch { }
            }
            else Errors[name] = Describe(code);
        }
        Refresh(false);
        painel.Redraw();
    }

    static string Describe(int code)
    {
        switch (code)
        {
            case -1: return "Sem resposta (tempo esgotado)";
            case 809: return "Erro 809 · o servidor não respondeu";
            case 868: return "Erro 868 · nome do servidor não resolve";
            case 812: return "Erro 812 · recusado pela política";
            case 13801: return "Erro 13801 · certificado do servidor";
            case 13806: return "Erro 13806 · certificado da máquina";
            case 623: case 1: return "Erro · ligação não encontrada";
            default: return "Erro " + code;
        }
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        icon.Visible = false; icon.Dispose();
        painel.Close();
        base.ExitThreadCore();
    }
}

class Painel : Form
{
    public static readonly string IconFont = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    static readonly string TextFont = FontExists("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    static bool FontExists(string n) { using (var f = new Font(n, 10)) return f.Name == n; }

    [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; public PT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)] struct SZ { public int cx, cy; public SZ(int a, int b) { cx = a; cy = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, fmt; }
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PT pos, ref SZ size, IntPtr src, ref PT srcPos, int key, ref BLEND b, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    readonly VpnCtx ctx;
    readonly float s;
    readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer();
    readonly List<KeyValuePair<RectangleF, string>> hits = new List<KeyValuePair<RectangleF, string>>();
    string hover;
    DateTime hiddenAt = DateTime.MinValue;
    bool wasDown;

    const float W = 330, Pad = 12, HeadH = 54, RowH = 54, FootH = 42, Radius = 24;

    public Painel(VpnCtx c)
    {
        ctx = c;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; KeyPreview = true;
        StartPosition = FormStartPosition.Manual;
        using (var g = CreateGraphics()) s = g.DpiX / 96f;
        watch.Interval = 60;
        watch.Tick += (o, e) => Watch();
        KeyDown += (o, e) => { if (e.KeyCode == Keys.Escape) HidePanel(); };
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x8; return cp; } // layered, toolwindow, topmost
    }
    protected override bool ShowWithoutActivation { get { return false; } }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    int HeightPx() { return (int)Math.Ceiling((HeadH + Math.Max(1, ctx.Entries.Count) * RowH + FootH + Pad) * s); }

    public void Toggle(Point near, int guardMs = 350)
    {
        if (Visible) { HidePanel(); return; }
        if ((DateTime.Now - hiddenAt).TotalMilliseconds < guardMs) return; // o clique que o fechou
        ShowNear(near);
    }

    public void ShowNear(Point near)
    {
        ctx.Refresh(true);
        var scr = Screen.FromPoint(near).Bounds;
        int w = (int)Math.Ceiling(W * s), h = HeightPx();
        int x = Math.Max(scr.Left + (int)(10 * s), Math.Min(near.X - w / 2, scr.Right - w - (int)(10 * s)));
        int y = Math.Min(near.Y - h - (int)(22 * s), scr.Bottom - h - (int)(84 * s));
        y = Math.Max(scr.Top + 10, y);
        Bounds = new Rectangle(x, y, w, h);
        hover = null;
        Show();
        Redraw();
        Activate(); SetForegroundWindow(Handle);
        wasDown = (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0;
        watch.Start();
    }

    void HidePanel()
    {
        if (!Visible) return;
        watch.Stop();
        Hide();
        hiddenAt = DateTime.Now;
    }

    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); HidePanel(); }

    // Fecha ao clicar fora (mesmo que o Windows não nos tenha dado o foco).
    void Watch()
    {
        bool down = (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0;
        if (down && !wasDown && !Bounds.Contains(Cursor.Position)) HidePanel();
        wasDown = down;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        string id = HitAt(e.Location);
        if (id != hover) { hover = id; Cursor = id != null ? Cursors.Hand : Cursors.Default; Redraw(); }
    }
    protected override void OnMouseLeave(EventArgs e) { if (hover != null) { hover = null; Redraw(); } }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        string id = HitAt(e.Location);
        if (id == null) return;
        if (id == ":settings") { HidePanel(); VpnCtx.OpenSettings(); }
        else ctx.ToggleVpn(id);
    }
    string HitAt(Point p)
    {
        foreach (var kv in hits) if (kv.Key.Contains(p.X / s, p.Y / s)) return kv.Value;
        return null;
    }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    public void Redraw()
    {
        if (!Visible || !IsHandleCreated) return;
        int h = HeightPx();
        if (h != Height) Height = h;
        int w = Width;
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                g.ScaleTransform(s, s);
                Draw(g, w / s, h / s);
            }
            Push(bmp);
        }
    }

    void Draw(Graphics g, float w, float h)
    {
        hits.Clear();
        Color text = Color.FromArgb(245, 255, 255, 255), sub = Color.FromArgb(150, 235, 238, 245);
        Color accent = Color.FromArgb(255, 10, 132, 255), green = Color.FromArgb(255, 48, 209, 88), amber = Color.FromArgb(255, 255, 179, 64), red = Color.FromArgb(255, 255, 105, 97);
        var card = new RectangleF(0.5f, 0.5f, w - 1, h - 1);
        using (var path = Round(card, Radius))
        {
            using (var b = new LinearGradientBrush(card, Color.FromArgb(246, 44, 47, 56), Color.FromArgb(246, 28, 30, 37), 90f)) g.FillPath(b, path);
            using (var p = new Pen(Color.FromArgb(40, 255, 255, 255), 1f)) g.DrawPath(p, path);
        }
        var sfL = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        using (var fTitle = new Font(TextFont, 15f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var fName = new Font(TextFont, 13.5f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var fSub = new Font(TextFont, 11.5f, GraphicsUnit.Pixel))
        using (var fIco = new Font(IconFont, 15f, GraphicsUnit.Pixel))
        using (var bText = new SolidBrush(text))
        using (var bSub = new SolidBrush(sub))
        {
            g.DrawString("VPN", fTitle, bText, new RectangleF(Pad + 8, 10, 120, 22), sfL);
            int n = ctx.Active.Count;
            g.DrawString(n == 0 ? "Nenhuma ligada" : n == 1 ? "1 ligada" : n + " ligadas", fSub, bSub, new RectangleF(Pad + 8, 30, 200, 16), sfL);

            float y = HeadH;
            if (ctx.Entries.Count == 0)
                g.DrawString("Não há VPNs configuradas no Windows", fSub, bSub, new RectangleF(Pad + 8, y, w - 2 * Pad, RowH), sfL);
            foreach (var name in ctx.Entries)
            {
                var row = new RectangleF(Pad - 4, y + 2, w - 2 * (Pad - 4), RowH - 4);
                bool on = ctx.Active.Contains(name), busy = ctx.Busy.ContainsKey(name), err = ctx.Errors.ContainsKey(name);
                if (hover == name) using (var rp = Round(row, 14)) using (var hb = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillPath(hb, rp);
                float cx = row.X + 26, cy = row.Y + row.Height / 2;
                using (var cb = new SolidBrush(on ? accent : Color.FromArgb(38, 255, 255, 255))) g.FillEllipse(cb, cx - 17, cy - 17, 34, 34);
                g.DrawString("", fIco, Brushes.White, new RectangleF(cx - 17, cy - 16, 34, 34), sfC);
                float tx = cx + 28, tw = row.Right - 64 - tx;
                g.DrawString(name, fName, bText, new RectangleF(tx, cy - 18, tw, 18), sfL);
                string st = busy ? ctx.Busy[name] : err ? ctx.Errors[name] : on ? "Ligada" : "Desligada";
                Color sc = busy ? amber : err ? red : on ? green : sub;
                using (var sb = new SolidBrush(sc)) g.DrawString(st, fSub, sb, new RectangleF(tx, cy + 1, tw + 40, 16), sfL);
                // interruptor
                var sw = new RectangleF(row.Right - 54, cy - 12, 42, 24);
                Color track = busy ? Color.FromArgb(200, amber.R, amber.G, amber.B) : on ? green : Color.FromArgb(60, 255, 255, 255);
                using (var sp = Round(sw, 12)) using (var tb = new SolidBrush(track)) g.FillPath(tb, sp);
                bool knobRight = busy ? !on : on;
                float kx = knobRight ? sw.Right - 22 : sw.X + 2;
                using (var kb = new SolidBrush(Color.White)) g.FillEllipse(kb, kx, sw.Y + 2, 20, 20);
                hits.Add(new KeyValuePair<RectangleF, string>(row, name));
                y += RowH;
            }
            // rodapé
            using (var p = new Pen(Color.FromArgb(28, 255, 255, 255), 1f)) g.DrawLine(p, Pad + 8, y + 4, w - Pad - 8, y + 4);
            var link = new RectangleF(Pad - 4, y + 8, w - 2 * (Pad - 4), FootH - 10);
            if (hover == ":settings") using (var rp = Round(link, 12)) using (var hb = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) g.FillPath(hb, rp);
            g.DrawString("Definições de VPN…", fSub, bText, new RectangleF(link.X + 12, link.Y, link.Width - 24, link.Height), sfL);
            hits.Add(new KeyValuePair<RectangleF, string>(link, ":settings"));
        }
    }

    void Push(Bitmap bmp)
    {
        IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), hb = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hb);
        try
        {
            var size = new SZ(bmp.Width, bmp.Height); var src = new PT(0, 0); var pos = new PT(Left, Top);
            var blend = new BLEND { op = 0, flags = 0, alpha = 255, fmt = 1 };
            UpdateLayeredWindow(Handle, screen, ref pos, ref size, mem, ref src, 0, ref blend, 2);
        }
        finally { SelectObject(mem, old); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen); }
    }
}

