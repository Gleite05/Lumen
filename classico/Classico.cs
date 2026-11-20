// Lumen Clássico — um clique e o PC volta ao Windows de sempre; outro clique e volta o Lumen.
//  original : fecha a barra e o Central do Lumen; mostra a barra do Windows (sem ocultar automático);
//             fundo e cursores do Windows; nada do Lumen arranca com o Windows.
//  lumen    : repõe tudo como estava (guardado em %LOCALAPPDATA%\Lumen\modo.json).
//  O atalho no ambiente de trabalho muda de nome e de ícone conforme o modo.
//
//  Lumen.Classico.exe                 alterna
//  Lumen.Classico.exe original|lumen  força um modo
//  Lumen.Classico.exe --instalar [--idioma pt-PT] [--tema esmeralda] [--cursores escuro] [--atalho]
//                                     primeira configuração (o instalador chama-o no fim)
//  Lumen.Classico.exe --parar         fecha tudo sem mudar nada (atualizações)
//  Lumen.Classico.exe --desinstalar   repõe o Windows de origem e tira o Lumen do arranque
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Classico
{
    static readonly string Estado = Path.Combine(Lumen.Dados, "modo.json");

    [STAThread]
    static void Main(string[] a)
    {
        bool nova;
        using (var m = new Mutex(true, "Local\\Lumen.Classico", out nova))
        {
            if (!nova) return;
            Directory.CreateDirectory(Lumen.Dados);
            var cfg = Lumen.Ler();
            Lumen.DefinirIdioma(cfg);
            string op = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            try
            {
                if (op == "--parar") { Aplicar.PararTudo(); return; }
                if (op == "--instalar") { Instalar(a, cfg); return; }
                if (op == "--desinstalar") { Desinstalar(); return; }
                var est = LerEstado();
                string atual = Lumen.S(est, "modo") == "original" ? "original" : "lumen";
                string alvo = op == "original" || op == "lumen" ? op : (atual == "original" ? "lumen" : "original");
                var aviso = new Aviso(alvo == "original" ? Lumen.T("A voltar ao Windows clássico…", "Going back to classic Windows…", "Volviendo a Windows clásico…")
                                                         : Lumen.T("A voltar ao Lumen…", "Back to Lumen…", "Volviendo a Lumen…"));
                try
                {
                    if (alvo == "original") ParaOriginal(est, cfg); else ParaLumen(est, cfg);
                    est["modo"] = alvo;
                    GravarEstado(est);
                    Atalho(alvo, false);
                    aviso.Fim(alvo == "original"
                        ? Lumen.T("Windows clássico. Clica outra vez no atalho para voltar ao Lumen.", "Classic Windows. Click the shortcut again to go back to Lumen.", "Windows clásico. Vuelve a pulsar el acceso para volver a Lumen.")
                        : Lumen.T("O Lumen está de volta.", "Lumen is back.", "Lumen ha vuelto."));
                }
                catch (Exception e) { aviso.Fim(Lumen.T("Algo correu mal: ", "Something went wrong: ", "Algo ha fallado: ") + e.Message); }
            }
            catch (Exception e) { Lumen.Log("classico", op + ": " + e); }
        }
    }

    static Dictionary<string, object> LerEstado()
    {
        try { if (File.Exists(Estado)) return Lumen.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Estado)) ?? new Dictionary<string, object>(); } catch { }
        return new Dictionary<string, object>();
    }
    static void GravarEstado(Dictionary<string, object> est) { File.WriteAllText(Estado, Lumen.Json.Serialize(est)); }

    static IEnumerable<string> Extras(Dictionary<string, object> cfg)
    {
        var l = Lumen.C(cfg, "fecharNoClassico") as ArrayList;
        return l == null ? new string[0] : l.Cast<object>().Select(o => Convert.ToString(o)).Where(s => s != "");
    }

    // ------------------------------------------------------------ para o Windows clássico
    static void ParaOriginal(Dictionary<string, object> est, Dictionary<string, object> cfg)
    {
        // 1. guarda o que está (só se viermos do Lumen)
        if (Lumen.S(est, "modo") != "original")
        {
            var run = new Dictionary<string, object>();
            using (var k = Registry.CurrentUser.OpenSubKey(Aplicar.RunKey))
                if (k != null) foreach (var n in new[] { Aplicar.RunNome }.Concat(Extras(cfg))) { var v = k.GetValue(n); if (v != null) run[n] = v.ToString(); }
            est["run"] = run;
            est["cursores"] = Aplicar.GuardarCursores();
            using (var adv = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                est["flash"] = adv == null || adv.GetValue("TaskbarFlashing") == null ? "" : adv.GetValue("TaskbarFlashing").ToString();
        }
        // 2. nada do Lumen arranca com o Windows
        using (var k = Registry.CurrentUser.CreateSubKey(Aplicar.RunKey))
            foreach (var n in new[] { Aplicar.RunNome }.Concat(Extras(cfg))) if (k.GetValue(n) != null) k.DeleteValue(n);
        Aplicar.Tarefa("/DISABLE");
        // 3. fecha tudo (o Central devolve a barra do Windows; a barra do Lumen repõe o "Não incomodar")
        Aplicar.PararCentral();
        foreach (var n in Extras(cfg)) Aplicar.Fechar(Path.GetFileNameWithoutExtension(n), null, 8);
        Aplicar.PararBarra();
        // 4. Windows de origem
        Aplicar.MostrarBarraWindows();
        Aplicar.SemIntermitencia(false);
        Aplicar.Cursores("windows");
        Aplicar.FundoWindows();
    }

    // ------------------------------------------------------------ de volta ao Lumen
    static void ParaLumen(Dictionary<string, object> est, Dictionary<string, object> cfg)
    {
        var run = Lumen.C(est, "run") as Dictionary<string, object>;
        using (var k = Registry.CurrentUser.CreateSubKey(Aplicar.RunKey))
        {
            if (run != null) foreach (var kv in run) k.SetValue(kv.Key, kv.Value.ToString());
            if (k.GetValue(Aplicar.RunNome) == null) k.SetValue(Aplicar.RunNome, "\"" + Lumen.Central + "\"");
        }
        Aplicar.Tarefa("/ENABLE");
        var cur = Lumen.C(est, "cursores") as Dictionary<string, object>;
        if (cur != null && cur.Count > 0) Aplicar.ReporCursores(cur); else Aplicar.Cursores(Lumen.S(cfg, "cursores"));
        Aplicar.SemIntermitencia(Lumen.S(est, "flash") != "");
        Aplicar.IniciarTudo(); // o Central põe o fundo
        // os teus outros programas que o modo clássico tinha fechado
        if (run != null)
            foreach (var kv in run)
            {
                if (kv.Key == Aplicar.RunNome) continue;
                string v = kv.Value.ToString();
                string exe = v.StartsWith("\"") ? v.Substring(1, v.IndexOf('"', 1) - 1) : v.Split(' ')[0];
                string args = v.StartsWith("\"") ? v.Substring(v.IndexOf('"', 1) + 1).Trim() : "";
                if (File.Exists(exe) && Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)).Length == 0) try { Process.Start(exe, args); } catch { }
            }
    }

    // ------------------------------------------------------------ instalar (fim do instalador)
    static void Instalar(string[] a, Dictionary<string, object> cfg)
    {
        Func<string, string> Arg = nome => { int i = Array.IndexOf(a, nome); return i >= 0 && i + 1 < a.Length ? a[i + 1] : ""; };
        bool primeira = !File.Exists(Lumen.Config);
        Migrar(ref cfg, primeira);
        if (Arg("--idioma") != "") cfg["idioma"] = Arg("--idioma");
        if (Arg("--tema") != "") cfg["tema"] = Arg("--tema");
        if (Arg("--cursores") != "") cfg["cursores"] = Arg("--cursores");
        Lumen.Gravar(cfg);
        Lumen.DefinirIdioma(cfg);

        // definições da barra: as do Lumen na primeira vez
        if (!File.Exists(Lumen.DefBarra))
        {
            string pre = Path.Combine(Lumen.Pasta, @"predefinicoes\settings.json");
            if (File.Exists(pre)) File.Copy(pre, Lumen.DefBarra);
            string wid = Path.Combine(Lumen.Pasta, @"predefinicoes\widgets.json");
            if (File.Exists(wid) && !File.Exists(Path.Combine(Lumen.Dados, "widgets.json"))) File.Copy(wid, Path.Combine(Lumen.Dados, "widgets.json"));
        }
        if (Arg("--idioma") != "") Aplicar.AlterarBarra(new Dictionary<string, object> { { "language", Lumen.TagBarra } });

        Aplicar.Arranque(true);
        Aplicar.SemIntermitencia(true);
        Aplicar.Cursores(Lumen.S(cfg, "cursores"));
        var est = LerEstado(); est["modo"] = "lumen"; GravarEstado(est);
        Atalho("lumen", Array.IndexOf(a, "--atalho") >= 0);
        Aplicar.IniciarTudo();
        Lumen.Log("classico", "instalado " + Lumen.Versao);
    }

    // Quem vinha do Atrium ou do "Windows 2.0" (a versão antes do Lumen) fica com tudo o que tinha.
    static void Migrar(ref Dictionary<string, object> cfg, bool primeira)
    {
        string L = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string atrium = Path.Combine(L, "Atrium");
        if (!File.Exists(Lumen.DefBarra) && File.Exists(Path.Combine(atrium, "settings.json")))
        {
            foreach (var f in new[] { "settings.json", "widgets.json", "start.json", "stage.json", "weather.json" })
                if (File.Exists(Path.Combine(atrium, f)) && !File.Exists(Path.Combine(Lumen.Dados, f))) File.Copy(Path.Combine(atrium, f), Path.Combine(Lumen.Dados, f));
            Lumen.Log("classico", "migradas as definições do Atrium");
        }
        string velho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".central\config.json");
        if (primeira && File.Exists(velho))
        {
            try
            {
                var v = Lumen.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(velho));
                foreach (var k in new[] { "horario", "dias", "feriados", "servidores", "atalhos", "area" }) if (v.ContainsKey(k)) cfg[k] = v[k];
                Lumen.Log("classico", "migrada a configuração do Central antigo");
            }
            catch (Exception e) { Lumen.Log("classico", "migração do Central: " + e.Message); }
        }
        // o que estava instalado antes deixa de arrancar (o Lumen substitui-o)
        Aplicar.Fechar("Central", () => Aplicar.Correr(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".central\Central.exe"), "--sair", 8000), 8);
        Aplicar.Fechar("BarraOculta", null, 5);
        Aplicar.Fechar("Atrium", () => Aplicar.Correr(Path.Combine(L, @"Programs\Atrium\Atrium\Atrium.exe"), "--exit", 30000), 30);
        using (var k = Registry.CurrentUser.CreateSubKey(Aplicar.RunKey))
            foreach (var n in new[] { "Central", "BarraOculta", "Atrium" }) if (k.GetValue(n) != null) k.DeleteValue(n);
        Aplicar.Tarefa("/DISABLE", "Atrium");
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var n in new[] { "Modo Windows original.lnk", "Modo Windows 2.0.lnk" }) try { File.Delete(Path.Combine(desk, n)); } catch { }
    }

    // ------------------------------------------------------------ desinstalar
    static void Desinstalar()
    {
        var cfg = Lumen.Ler();
        var est = LerEstado();
        if (Lumen.S(est, "modo") != "original") ParaOriginal(est, cfg);
        Aplicar.Arranque(false);
        Aplicar.Tarefa("/DELETE");
        using (var k = Registry.CurrentUser.CreateSubKey(Aplicar.RunKey)) if (k.GetValue("Lumen") != null) k.DeleteValue("Lumen");
        // a barra regista-se em "Aplicações instaladas" quando corre solta: tira esse registo também
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Lumen", false); } catch { }
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var n in NomesAtalho()) try { File.Delete(Path.Combine(desk, n)); } catch { }
        try { File.Delete(Estado); } catch { }
        Lumen.Log("classico", "desinstalado");
    }

    // ------------------------------------------------------------ atalho do ambiente de trabalho
    static string[] NomesAtalho()
    {
        return new[] { "Modo Windows clássico.lnk", "Voltar ao Lumen.lnk", "Classic Windows mode.lnk", "Back to Lumen.lnk", "Modo Windows clásico.lnk", "Volver a Lumen.lnk" };
    }

    // O nome diz para onde vai o próximo clique. "criar" = criá-lo se ainda não existir.
    static void Atalho(string modo, bool criar)
    {
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var existentes = NomesAtalho().Select(n => Path.Combine(desk, n)).Where(File.Exists).ToList();
        if (existentes.Count == 0 && !criar) return;
        string nome = modo == "original" ? Lumen.T("Voltar ao Lumen", "Back to Lumen", "Volver a Lumen") : Lumen.T("Modo Windows clássico", "Classic Windows mode", "Modo Windows clásico");
        string novo = Path.Combine(desk, nome + ".lnk");
        string icone = Path.Combine(Lumen.Pasta, modo == "original" ? "lumen.ico" : "windows-classico.ico");
        try
        {
            foreach (var e in existentes) if (!string.Equals(e, novo, StringComparison.OrdinalIgnoreCase)) File.Delete(e);
            dynamic s = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic lnk = s.CreateShortcut(novo);
            lnk.TargetPath = Lumen.Classico;
            lnk.WorkingDirectory = Lumen.Pasta;
            lnk.Description = modo == "original" ? Lumen.T("Volta ao Lumen", "Back to Lumen", "Vuelve a Lumen") : Lumen.T("Volta ao Windows de sempre", "Back to plain Windows", "Vuelve al Windows de siempre");
            if (File.Exists(icone)) lnk.IconLocation = icone + ",0";
            lnk.Save();
        }
        catch (Exception e) { Lumen.Log("classico", "atalho: " + e.Message); }
    }
}

// Um aviso de vidro escuro no canto enquanto trabalha; fecha-se sozinho.
class Aviso
{
    readonly Form f;
    readonly Label t;
    public Aviso(string texto)
    {
        f = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true, StartPosition = FormStartPosition.Manual,
            BackColor = Color.FromArgb(18, 24, 22), Size = new Size(460, 76), Opacity = 0.97 };
        var ic = new PictureBox { Size = new Size(40, 40), Location = new Point(18, 18), SizeMode = PictureBoxSizeMode.Zoom };
        try { ic.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
        t = new Label { Location = new Point(70, 0), Size = new Size(372, 76), ForeColor = Color.White, Font = new Font("Segoe UI", 10.5f), TextAlign = ContentAlignment.MiddleLeft, Text = texto };
        f.Controls.Add(ic); f.Controls.Add(t);
        var gp = new GraphicsPath(); int r = 18;
        gp.AddArc(0, 0, r * 2, r * 2, 180, 90); gp.AddArc(f.Width - r * 2, 0, r * 2, r * 2, 270, 90);
        gp.AddArc(f.Width - r * 2, f.Height - r * 2, r * 2, r * 2, 0, 90); gp.AddArc(0, f.Height - r * 2, r * 2, r * 2, 90, 90); gp.CloseFigure();
        f.Region = new Region(gp);
        var wa = Screen.PrimaryScreen.WorkingArea;
        f.Location = new Point(wa.Right - f.Width - 24, wa.Top + 24);
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
