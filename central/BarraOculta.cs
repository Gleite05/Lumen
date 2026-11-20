// Barra oculta — mantém a barra de tarefas do Windows escondida de vez (a do Lumen fica no lugar dela).
// Ctrl+Alt+B mostra-a (até a usares e ela voltar a recolher); Ctrl+Alt+B outra vez esconde já.
// Atua só nas janelas Shell_TrayWnd / Shell_SecondaryTrayWnd do explorer.exe
// (a barra do Lumen regista um Shell_TrayWnd próprio, que é ignorado).
// Ao parar, a barra do Windows volta a ficar visível (em ocultar automático).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class BarraOculta : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
    [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint cb; public uint edge; public RECT rc; public IntPtr lParam; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
    const int SW_HIDE = 0, SW_SHOWNA = 8;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
    const int HOTKEY_ID = 0xB0B;

    class MsgWindow : NativeWindow
    {
        public Action<Message> OnMsg;
        public MsgWindow() { CreateHandle(new CreateParams { Caption = "Lumen.BarraOculta" }); }
        protected override void WndProc(ref Message m) { if (OnMsg != null) OnMsg(m); base.WndProc(ref m); }
    }

    readonly MsgWindow win = new MsgWindow();
    readonly Timer timer = new Timer();
    readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    List<IntPtr> trays = new List<IntPtr>();
    DateTime lastFind = DateTime.MinValue, lastAutoHide = DateTime.MinValue;
    bool showMode, wasRevealed;
    DateTime showStart, hiddenSince = DateTime.MaxValue;
    public bool AtalhoLivre { get; private set; }
    public bool AMostrar { get { return showMode; } }
    public event Action Mudou;

    public BarraOculta()
    {
        AtalhoLivre = RegisterHotKey(win.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.B);
        win.OnMsg = m =>
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_ID) Alternar();
            else if (taskbarCreated != 0 && m.Msg == (int)taskbarCreated) { lastFind = DateTime.MinValue; lastAutoHide = DateTime.MinValue; }
        };
        timer.Interval = 200;
        timer.Tick += (s, e) => Tick();
        timer.Start();
        Tick();
    }

    static List<IntPtr> FindTrays()
    {
        var list = new List<IntPtr>();
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName("explorer")) { pids.Add((uint)p.Id); p.Dispose(); }
        EnumWindows((h, l) =>
        {
            var sb = new StringBuilder(64); GetClassName(h, sb, 64);
            string c = sb.ToString();
            if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd")
            {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pids.Contains(pid)) list.Add(h);
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    IntPtr Primary()
    {
        foreach (var t in trays)
        {
            var sb = new StringBuilder(64); GetClassName(t, sb, 64);
            if (sb.ToString() == "Shell_TrayWnd") return t;
        }
        return IntPtr.Zero;
    }

    static void EnsureAutoHide(IntPtr p)
    {
        var d = new APPBARDATA(); d.cbSize = Marshal.SizeOf(d); d.hWnd = p;
        if (((uint)SHAppBarMessage(4, ref d) & 1) == 0) { d.lParam = (IntPtr)1; SHAppBarMessage(10, ref d); }
    }

    void Tick()
    {
        var now = DateTime.Now;
        if ((now - lastFind).TotalSeconds > 2 || trays.Exists(t => !IsWindow(t))) { trays = FindTrays(); lastFind = now; }
        IntPtr p = Primary();
        if (p != IntPtr.Zero && (now - lastAutoHide).TotalSeconds > 5) { EnsureAutoHide(p); lastAutoHide = now; }

        if (!showMode)
        {
            foreach (var t in trays) if (IsWindowVisible(t)) ShowWindow(t, SW_HIDE);
            return;
        }
        // modo "mostrar": volta a esconder de vez quando a barra recolher depois de usada
        if (p == IntPtr.Zero) return;
        RECT r; GetWindowRect(p, out r);
        bool revealed = r.t < Screen.PrimaryScreen.Bounds.Bottom - 8;
        if (revealed) { wasRevealed = true; hiddenSince = DateTime.MaxValue; }
        else if (wasRevealed)
        {
            if (hiddenSince == DateTime.MaxValue) hiddenSince = now;
            else if ((now - hiddenSince).TotalMilliseconds > 1200) Esconder();
        }
        else if ((now - showStart).TotalSeconds > 5) Esconder();
    }

    public void Alternar() { if (showMode) Esconder(); else Mostrar(); }

    void Mostrar()
    {
        trays = FindTrays(); lastFind = DateTime.Now;
        showMode = true; wasRevealed = false; hiddenSince = DateTime.MaxValue; showStart = DateTime.Now;
        foreach (var t in trays) ShowWindow(t, SW_SHOWNA);
        IntPtr p = Primary();
        if (p != IntPtr.Zero) SetForegroundWindow(p); // a barra em ocultar automático sobe enquanto tiver o foco
        if (Mudou != null) Mudou();
    }

    void Esconder()
    {
        showMode = false;
        foreach (var t in trays) if (IsWindowVisible(t)) ShowWindow(t, SW_HIDE);
        if (Mudou != null) Mudou();
    }

    public void Dispose()
    {
        timer.Stop(); timer.Dispose();
        UnregisterHotKey(win.Handle, HOTKEY_ID);
        foreach (var t in FindTrays()) ShowWindow(t, SW_SHOWNA);
        win.DestroyHandle();
    }
}
