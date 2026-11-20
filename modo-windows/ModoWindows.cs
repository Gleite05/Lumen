// ModoWindows — um clique e o PC volta ao Windows de origem; outro clique e volta o Windows 2.0.
//  Original: fecha o Atrium, a BarraOculta, o Central e a barra da voz; mostra a barra do Windows
//            (sem ocultar automático); fundo e cursores de origem; nada disto arranca com o Windows.
//  Windows 2.0: repõe tudo como estava (guardado em %USERPROFILE%\.central\modo.json).
//  O atalho no ambiente de trabalho muda de nome e de ícone conforme o modo.
//  ModoWindows.exe            alterna
//  ModoWindows.exe original   |  ModoWindows.exe atrium   força um modo
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

static class Modo
{
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".central");
    static readonly string Estado = Path.Combine(Pasta, "modo.json");
    static readonly string L = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string AtriumExe = Path.Combine(L, @"Programs\Atrium\Atrium\Atrium.exe");
    static readonly string Barra = Path.Combine(L, @"Programs\Atrium-extras\BarraOculta.exe");
    static readonly string CentralExe = Path.Combine(Pasta, "Central.exe");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static readonly string[] Residentes = { "BarraOculta", "Central", "VozBarra" };   // entradas de arranque que saem no modo original
    static readonly string[] Cursores = { "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person" };
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);

    [STAThread]
    static void Main(string[] a)
    {
        bool nova;
        using (var m = new Mutex(true, "Local\\ModoWindows.Gleite05", out nova))
        {
            if (!nova) return;
            Directory.CreateDirectory(Pasta);
            var est = Ler();
            string atual = est.ContainsKey("modo") ? (string)est["modo"] : "atrium";
            string alvo = a.Length > 0 ? a[0].ToLowerInvariant() : (atual == "original" ? "atrium" : "original");
            var aviso = new Aviso(alvo == "original" ? "A voltar ao Windows de origem…" : "A voltar ao Windows 2.0…");
            try
            {
                if (alvo == "original") ParaOriginal(est); else ParaAtrium(est);
                est["modo"] = alvo == "original" ? "original" : "atrium";
                File.WriteAllText(Estado, Json.Serialize(est));
                Atalho(est["modo"].ToString());
                aviso.Fim(alvo == "original" ? "Windows de origem. Clica outra vez no ícone para voltar ao Windows 2.0." : "Windows 2.0 de volta.");
            }
            catch (Exception e) { aviso.Fim("Algo correu mal: " + e.Message); }
        }
    }

    static Dictionary<string, object> Ler()
    {
        try { if (File.Exists(Estado)) return Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Estado)); } catch { }
        return new Dictionary<string, object>();
    }

    // ------------------------------------------------------------ para o Windows de origem
    static void ParaOriginal(Dictionary<string, object> est)
    {
        // 1. guarda o que está configurado (só se viermos do modo Windows 2.0)
        if (!est.ContainsKey("modo") || (string)est["modo"] != "original")
        {
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey))
                est["run"] = Residentes.Where(n => run.GetValue(n) != null).ToDictionary(n => n, n => (object)run.GetValue(n).ToString());
            using (var c = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
            {
                var d = new Dictionary<string, object>();
                foreach (var n in Cursores.Concat(new[] { "" })) d[n == "" ? "(default)" : n] = (c.GetValue(n) ?? "").ToString();
                est["cursores"] = d;
            }
            using (var adv = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                est["flash"] = adv.GetValue("TaskbarFlashing") == null ? "" : adv.GetValue("TaskbarFlashing").ToString();
        }

        // 2. nada disto arranca com o Windows
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            foreach (var n in Residentes) if (run.GetValue(n) != null) run.DeleteValue(n);
        Schtasks("/DISABLE");

        // 3. fecha tudo
        Fechar("Central", () => Correr(CentralExe, "--sair"));
        Fechar("VozBarra", null);
        Fechar("BarraOculta", null);
        Fechar("Atrium", () => Correr(AtriumExe, "--exit", 30000));

        // 4. barra do Windows visível e sem ocultar automático
        MostrarBarra();
        using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
            if (adv.GetValue("TaskbarFlashing") != null) adv.DeleteValue("TaskbarFlashing");
        Avisar("TraySettings");

        // 5. cursores e fundo de origem
        using (var c = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
        {
            foreach (var n in Cursores) c.SetValue(n, "", RegistryValueKind.ExpandString);
            c.SetValue("", "Windows Default");
            c.SetValue("Scheme Source", 2, RegistryValueKind.DWord);
        }
        SystemParametersInfo(0x57, 0, null, 3);
        string fundo = new[] { @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Web\Wallpaper\Windows\img19.jpg" }.FirstOrDefault(File.Exists);
        if (fundo != null)
        {
            using (var d = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")) { d.SetValue("WallpaperStyle", "10"); d.SetValue("TileWallpaper", "0"); }
            SystemParametersInfo(0x14, 0, fundo, 3);
        }
    }

    // ------------------------------------------------------------ de volta ao Windows 2.0
    static void ParaAtrium(Dictionary<string, object> est)
    {
        var run = est.ContainsKey("run") ? est["run"] as Dictionary<string, object> : null;
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (run != null && run.Count > 0) foreach (var kv in run) k.SetValue(kv.Key, kv.Value.ToString());
            else
            {
                if (File.Exists(Barra)) k.SetValue("BarraOculta", "\"" + Barra + "\"");
                if (File.Exists(CentralExe)) k.SetValue("Central", "\"" + CentralExe + "\"");
            }
        }
        Schtasks("/ENABLE");

        var cur = est.ContainsKey("cursores") ? est["cursores"] as Dictionary<string, object> : null;
        if (cur != null)
        {
            using (var c = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
            {
                foreach (var kv in cur)
                    if (kv.Key == "(default)") c.SetValue("", kv.Value.ToString());
                    else c.SetValue(kv.Key, kv.Value.ToString(), RegistryValueKind.ExpandString);
                c.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
            }
            SystemParametersInfo(0x57, 0, null, 3);
        }
        string flash = est.ContainsKey("flash") ? est["flash"].ToString() : "0";
        if (flash != "")
            using (var adv = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                adv.SetValue("TaskbarFlashing", int.Parse(flash), RegistryValueKind.DWord);
        Avisar("TraySettings");

        // arranca pela ordem do arranque normal: Atrium, BarraOculta, Central (que põe o fundo), barra da voz
        if (File.Exists(AtriumExe) && Process.GetProcessesByName("Atrium").Length == 0) Process.Start(AtriumExe);
        Thread.Sleep(2500);
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            foreach (var n in new[] { "BarraOculta", "Central", "VozBarra" })
            {
                var v = k.GetValue(n) as string;
                if (string.IsNullOrEmpty(v) || Process.GetProcessesByName(n).Length > 0) continue;
                string exe = v.StartsWith("\"") ? v.Substring(1, v.IndexOf('"', 1) - 1) : v.Split(' ')[0];
                string args = v.StartsWith("\"") ? v.Substring(v.IndexOf('"', 1) + 1).Trim() : "";
                if (File.Exists(exe)) Process.Start(exe, args);
            }
    }

    // ------------------------------------------------------------ ajudas
    static void Fechar(string nome, Action educado)
    {
        var ps = Process.GetProcessesByName(nome);
        if (ps.Length == 0) return;
        if (educado != null) try { educado(); } catch { }
        foreach (var p in Process.GetProcessesByName(nome)) try { p.CloseMainWindow(); } catch { }
        var ate = DateTime.Now.AddSeconds(nome == "Atrium" ? 30 : 8);
        while (DateTime.Now < ate && Process.GetProcessesByName(nome).Length > 0) Thread.Sleep(300);
        foreach (var p in Process.GetProcessesByName(nome)) try { p.Kill(); } catch { }
    }

    static void Correr(string exe, string args, int esperaMs = 10000)
    {
        if (!File.Exists(exe)) return;
        using (var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true })) p.WaitForExit(esperaMs);
    }

    static void Schtasks(string acao)
    {
        try
        {
            string sid = WindowsIdentity.GetCurrent().User.Value;
            var psi = new ProcessStartInfo("schtasks.exe", "/Change /TN \"Atrium Startup-" + sid + "\" " + acao) { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(psi)) p.WaitForExit(10000);
        }
        catch { }
    }

    static void MostrarBarra()
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

    static void Avisar(string area) { UIntPtr r; SendMessageTimeout(new IntPtr(0xffff), 0x1A, UIntPtr.Zero, area, 2, 2000, out r); }

    // O atalho do ambiente de trabalho diz para onde vai o próximo clique.
    static void Atalho(string modo)
    {
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string paraOriginal = Path.Combine(desk, "Modo Windows original.lnk"), paraAtrium = Path.Combine(desk, "Modo Windows 2.0.lnk");
        string novo = modo == "original" ? paraAtrium : paraOriginal, velho = modo == "original" ? paraOriginal : paraAtrium;
        string icone = Path.Combine(Pasta, modo == "original" ? "windows2.ico" : "windows-original.ico");
        try
        {
            if (File.Exists(velho)) File.Move(velho, novo);
            var sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic s = sh;
            dynamic lnk = s.CreateShortcut(novo);
            lnk.TargetPath = Application.ExecutablePath;
            lnk.WorkingDirectory = Pasta;
            lnk.Description = modo == "original" ? "Volta ao Windows 2.0 (Atrium, Central…)" : "Volta ao Windows de origem";
            if (File.Exists(icone)) lnk.IconLocation = icone + ",0";
            lnk.Save();
        }
        catch { }
    }
}

// Um aviso pequeno no canto enquanto trabalha; fecha-se sozinho.
class Aviso
{
    readonly Form f;
    readonly Label t;
    public Aviso(string texto)
    {
        f = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true, StartPosition = FormStartPosition.Manual, BackColor = System.Drawing.Color.FromArgb(24, 28, 32), Size = new System.Drawing.Size(420, 70), Opacity = 0.96 };
        t = new Label { Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 11f), TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Text = texto };
        f.Controls.Add(t);
        var wa = Screen.PrimaryScreen.WorkingArea;
        f.Location = new System.Drawing.Point(wa.Right - f.Width - 24, wa.Top + 24);
        f.Show(); Application.DoEvents();
    }
    public void Fim(string texto)
    {
        t.Text = texto; Application.DoEvents();
        var fim = DateTime.Now.AddSeconds(3.5);
        while (DateTime.Now < fim) { Application.DoEvents(); Thread.Sleep(50); }
        f.Close();
    }
}
