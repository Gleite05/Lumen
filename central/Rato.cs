// Rato: os painéis ficam por baixo dos ícones do ambiente de trabalho, por isso o rato chega primeiro
// à lista dos ícones. Um gancho de baixo nível (WH_MOUSE_LL) vê onde o rato está e, se for espaço vazio
// do ambiente de trabalho por cima da área dos painéis, entrega o movimento e os cliques também à página
// e a página faz o realce e o clique. Num ícone, o clique é só do ícone.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Web.WebView2.WinForms;

static class Rato
{
    delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct MSLL { public int x, y; public uint data, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct HIT { public int x, y; public uint flags; public int item, sub, group; }
    [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; }

    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string n);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(PT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref PT p);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr s, uint t, uint pr);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, ref HIT b, UIntPtr s, out UIntPtr w);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, out HIT b, UIntPtr s, out UIntPtr r);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_MOUSELEAVE = 0x2A3;
    static HookProc proc;
    static IntPtr hook, alvo;
    static WebView2 web;
    static Janela janela;
    static int ultimoMovimento;
    static Rectangle area;
    static bool ativo, dentro, premido;
    static uint pidExplorer;
    static IntPtr procExplorer, memoria;

    public static void Ligar(Janela j, WebView2 w, Rectangle a, bool porBaixo)
    {
        janela = j; web = w; area = a; ativo = porBaixo;
        if (porBaixo && hook == IntPtr.Zero)
        {
            proc = Gancho;
            hook = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0); // WH_MOUSE_LL
            if (hook == IntPtr.Zero) P.Log("rato: o gancho não ficou instalado (" + Marshal.GetLastWin32Error() + ")");
        }
    }

    static IntPtr Gancho(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0 && ativo)
        {
            int m = w.ToInt32();
            if (m == WM_MOUSEMOVE || m == WM_LBUTTONDOWN || m == WM_LBUTTONUP)
            {
                var d = (MSLL)Marshal.PtrToStructure(l, typeof(MSLL));
                try { Tratar(m, new PT { x = d.x, y = d.y }); } catch { }
            }
        }
        return CallNextHookEx(hook, code, w, l);
    }

    static void Tratar(int m, PT p)
    {
        IntPtr sob = WindowFromPoint(p);
        bool vazio = area.Contains(p.x, p.y) && EAmbienteDeTrabalho(sob);
        if (m == WM_LBUTTONUP && premido) { premido = false; if (vazio) Pagina("clique", p); return; }
        if (!vazio)
        {
            if (dentro) { dentro = false; Pagina("sair", p); }
            return;
        }
        dentro = true;
        if (m == WM_MOUSEMOVE)
        {
            if (Environment.TickCount - ultimoMovimento < 30) return; // ~30 por segundo chegam
            ultimoMovimento = Environment.TickCount;
            Pagina("mover", p);
        }
        else if (m == WM_LBUTTONDOWN && !SobreIcone(sob, p)) premido = true;
    }

    static bool EAmbienteDeTrabalho(IntPtr h)
    {
        string c = Classe(h);
        if (c == "SHELLDLL_DefView") return true;
        return c == "SysListView32" && Classe(GetParent(h)) == "SHELLDLL_DefView";
    }

    static string Classe(IntPtr h) { var s = new StringBuilder(64); GetClassName(h, s, 64); return s.ToString(); }

    // A janela que recebe o rato dentro do WebView2.
    static IntPtr Alvo()
    {
        if (alvo != IntPtr.Zero && IsWindow(alvo)) return alvo;
        alvo = IntPtr.Zero;
        if (web == null || !web.IsHandleCreated) return IntPtr.Zero;
        EnumChildWindows(web.Handle, (h, l) => { if (Classe(h) == "Chrome_RenderWidgetHostHWND") { alvo = h; return false; } return true; }, IntPtr.Zero);
        return alvo;
    }

    // O Chromium do WebView2 ignora rato injetado por mensagens: a página recebe o ponto (px CSS)
    // e faz ela o realce e o clique no elemento que lá estiver.
    static void Pagina(string ev, PT p)
    {
        if (janela == null) return;
        float k = janela.DeviceDpi / 96f;
        janela.Enviar(new System.Collections.Generic.Dictionary<string, object> {
            { "tipo", "rato" }, { "ev", ev }, { "x", (p.x - area.X) / k }, { "y", (p.y - area.Y) / k } });
    }

    // LVM_HITTEST na lista dos ícones (vive no explorer.exe: a estrutura vai pela memória dele).
    static bool SobreIcone(IntPtr lista, PT p)
    {
        if (Classe(lista) != "SysListView32") return false;
        try
        {
            uint pid; GetWindowThreadProcessId(lista, out pid);
            if (pid != pidExplorer || memoria == IntPtr.Zero)
            {
                if (procExplorer != IntPtr.Zero) CloseHandle(procExplorer);
                procExplorer = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid); // VM_OPERATION | VM_READ | VM_WRITE
                memoria = procExplorer == IntPtr.Zero ? IntPtr.Zero : VirtualAllocEx(procExplorer, IntPtr.Zero, new UIntPtr(4096), 0x3000, 0x04);
                pidExplorer = pid;
            }
            if (memoria == IntPtr.Zero) return true; // sem forma de saber: o clique fica só para o ambiente de trabalho
            var c = p; ScreenToClient(lista, ref c);
            var h = new HIT { x = c.x, y = c.y, item = -1 };
            UIntPtr n, tam = new UIntPtr((uint)Marshal.SizeOf(typeof(HIT)));
            if (!WriteProcessMemory(procExplorer, memoria, ref h, tam, out n)) { memoria = IntPtr.Zero; return true; }
            IntPtr r;
            if (SendMessageTimeout(lista, 0x1012, IntPtr.Zero, memoria, 0x0002, 100, out r) == IntPtr.Zero) return true;
            ReadProcessMemory(procExplorer, memoria, out h, tam, out n);
            return h.item >= 0 && (h.flags & 0x0E) != 0;
        }
        catch { return true; }
    }
}
