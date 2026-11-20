// Lumen — o que mexe no Windows: cursores, arranque, definições da barra, arrancar e parar as peças.
// Usado pelo Painel e pelo modo clássico (que também é o "instalar" e o "desinstalar").
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

static class Aplicar
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunNome = "Lumen";
    public static readonly string[] NomesCursores = { "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person" };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
    [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);

    // ------------------------------------------------------------ cursores
    // escuro | claro | windows
    public static void Cursores(string estilo)
    {
        string pasta = estilo == "claro" ? "Fluent-Claro" : estilo == "escuro" ? "Fluent-Escuro" : null;
        string dir = pasta == null ? null : Path.Combine(Lumen.Pasta, "cursores", pasta);
        if (dir != null && !Directory.Exists(dir)) dir = null;
        using (var c = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
        {
            foreach (var n in NomesCursores)
            {
                string f = dir == null ? "" : new[] { ".cur", ".ani" }.Select(x => Path.Combine(dir, n + x)).FirstOrDefault(File.Exists) ?? "";
                c.SetValue(n, f, RegistryValueKind.ExpandString);
            }
            c.SetValue("", dir == null ? "Windows Default" : (estilo == "claro" ? "Lumen claro" : "Lumen escuro"));
            c.SetValue("Scheme Source", dir == null ? 2 : 1, RegistryValueKind.DWord);
        }
        SystemParametersInfo(0x57, 0, null, 3); // SPI_SETCURSORS
    }

    public static Dictionary<string, object> GuardarCursores()
    {
        var d = new Dictionary<string, object>();
        using (var c = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
            if (c != null) foreach (var n in NomesCursores.Concat(new[] { "" })) d[n == "" ? "(default)" : n] = (c.GetValue(n, "", RegistryValueOptions.DoNotExpandEnvironmentNames) ?? "").ToString();
        return d;
    }

    public static void ReporCursores(Dictionary<string, object> d)
    {
        if (d == null) return;
        using (var c = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
        {
            foreach (var kv in d)
                if (kv.Key == "(default)") c.SetValue("", kv.Value.ToString());
                else c.SetValue(kv.Key, kv.Value.ToString(), RegistryValueKind.ExpandString);
            c.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
        }
        SystemParametersInfo(0x57, 0, null, 3);
    }

    // ------------------------------------------------------------ arranque com o Windows
    public static bool ArranqueLigado()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunNome) != null;
    }

    public static void Arranque(bool ligado)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (ligado) k.SetValue(RunNome, "\"" + Lumen.Central + "\"");
            else if (k.GetValue(RunNome) != null) k.DeleteValue(RunNome);
        }
        // a tarefa de arranque da barra acompanha
        Tarefa(ligado ? "/ENABLE" : "/DISABLE");
    }

    public static void Tarefa(string acao, string produto = "Lumen")
    {
        try
        {
            string sid = WindowsIdentity.GetCurrent().User.Value;
            string args = acao == "/DELETE" ? "/Delete /TN \"" + produto + " Startup-" + sid + "\" /F" : "/Change /TN \"" + produto + " Startup-" + sid + "\" " + acao;
            var psi = new ProcessStartInfo("schtasks.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi)) p.WaitForExit(10000);
        }
        catch { }
    }

    // A barra a piscar (laranja) quando uma app quer atenção: o Lumen tem a sua própria luz.
    public static void SemIntermitencia(bool sem)
    {
        using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
        {
            if (sem) adv.SetValue("TaskbarFlashing", 0, RegistryValueKind.DWord);
            else if (adv.GetValue("TaskbarFlashing") != null) adv.DeleteValue("TaskbarFlashing");
        }
        AvisarWindows("TraySettings");
    }

    // ------------------------------------------------------------ definições da barra (settings.json)
    public static Dictionary<string, object> LerBarra()
    {
        try { if (File.Exists(Lumen.DefBarra)) return Lumen.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Lumen.DefBarra, Encoding.UTF8)) ?? new Dictionary<string, object>(); }
        catch { }
        return new Dictionary<string, object>();
    }

    // A barra regrava o seu ficheiro: para mudar uma opção é preciso pará-la, escrever e voltar a abri-la.
    public static void AlterarBarra(Dictionary<string, object> mudancas)
    {
        var atual = LerBarra();
        bool diferente = mudancas.Any(kv => !atual.ContainsKey(kv.Key) || !Equals(Convert.ToString(atual[kv.Key]), Convert.ToString(kv.Value)));
        if (!diferente) return;
        bool aCorrer = Process.GetProcessesByName("Lumen").Length > 0;
        if (aCorrer) PararBarra();
        atual = LerBarra();
        foreach (var kv in mudancas) atual[kv.Key] = kv.Value;
        Directory.CreateDirectory(Lumen.Dados);
        File.WriteAllText(Lumen.DefBarra, Lumen.Bonito(Lumen.Json.Serialize(atual)), new UTF8Encoding(false));
        if (aCorrer) IniciarBarra();
    }

    // ------------------------------------------------------------ arrancar e parar
    public static void IniciarBarra()
    {
        if (File.Exists(Lumen.Barra) && Process.GetProcessesByName("Lumen").Length == 0) Process.Start(Lumen.Barra);
    }

    public static void PararBarra() { Fechar("Lumen", () => Correr(Lumen.Barra, "--exit", 30000), 30); }

    public static void IniciarCentral()
    {
        if (File.Exists(Lumen.Central) && Process.GetProcessesByName("Lumen.Central").Length == 0) Process.Start(Lumen.Central);
    }

    public static void PararCentral() { Fechar("Lumen.Central", () => Correr(Lumen.Central, "--sair", 10000), 8); }

    public static void IniciarTudo() { IniciarBarra(); Thread.Sleep(1500); IniciarCentral(); }
    public static void PararTudo() { PararCentral(); PararBarra(); }

    public static void Fechar(string nome, Action educado, int segundos)
    {
        if (Process.GetProcessesByName(nome).Length == 0) return;
        if (educado != null) try { educado(); } catch { }
        var ate = DateTime.Now.AddSeconds(segundos);
        while (DateTime.Now < ate && Process.GetProcessesByName(nome).Length > 0) Thread.Sleep(300);
        foreach (var p in Process.GetProcessesByName(nome)) try { p.Kill(); } catch { }
    }

    public static void Correr(string exe, string args, int esperaMs)
    {
        if (!File.Exists(exe)) return;
        using (var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit(esperaMs);
    }

    // ------------------------------------------------------------ barra do Windows
    public static void MostrarBarraWindows()
    {
        var bandas = new List<IntPtr>();
        var explorer = new HashSet<uint>(Process.GetProcessesByName("explorer").Select(p => (uint)p.Id));
        EnumWindows((h, l) =>
        {
            var sb = new StringBuilder(64); GetClassName(h, sb, 64);
            var c = sb.ToString(); uint pid; GetWindowThreadProcessId(h, out pid);
            if ((c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") && explorer.Contains(pid)) bandas.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var h in bandas)
        {
            ShowWindow(h, 5);
            var d = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)), hWnd = h, lParam = new IntPtr(2) }; // ABS_ALWAYSONTOP: sem ocultar automático
            SHAppBarMessage(0x0A, ref d);
        }
    }

    public static void AvisarWindows(string area) { UIntPtr r; SendMessageTimeout(new IntPtr(0xffff), 0x1A, UIntPtr.Zero, area, 2, 2000, out r); }

    public static void FundoWindows()
    {
        string fundo = new[] { @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg" }.FirstOrDefault(File.Exists);
        if (fundo == null) return;
        using (var d = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")) { d.SetValue("WallpaperStyle", "10"); d.SetValue("TileWallpaper", "0"); }
        SystemParametersInfo(0x14, 0, fundo, 3);
    }

    public static void Fundo(string ficheiro)
    {
        if (!File.Exists(ficheiro)) return;
        using (var d = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")) { d.SetValue("WallpaperStyle", "10"); d.SetValue("TileWallpaper", "0"); }
        SystemParametersInfo(0x14, 0, ficheiro, 3);
    }
}
