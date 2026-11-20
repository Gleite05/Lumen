// Guarda: protege o resto do "Windows 2.0".
//  * Guarda uma cópia boa das definições do Atrium sempre que mudam (e não parecem de fábrica).
//  * Se o Atrium aparecer com as definições de fábrica, fecha-o, repõe a cópia e volta a abri-lo.
//  * Nos primeiros minutos depois de arrancar, garante que a BarraOculta e o Atrium estão a correr.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

static class Guarda
{
    static readonly string L = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Def = Path.Combine(L, @"Atrium\settings.json");
    static readonly string Wid = Path.Combine(L, @"Atrium\widgets.json");
    static readonly string Atrium = Path.Combine(L, @"Programs\Atrium\Atrium\Atrium.exe");
    static readonly string Barra = Path.Combine(L, @"Programs\Atrium-extras\BarraOculta.exe");
    static readonly string CopiaDef = Path.Combine(P.Pasta, "atrium-settings.bom.json");
    static readonly string CopiaWid = Path.Combine(P.Pasta, "atrium-widgets.bom.json");
    public static Action<string, string> Avisar = (t, m) => { };

    public static void Iniciar()
    {
        if (P.S(P.Cfg, "guarda") == "False") return;
        new Thread(() =>
        {
            var inicio = DateTime.Now;
            Thread.Sleep(20000);
            while (true)
            {
                try
                {
                    if ((DateTime.Now - inicio).TotalMinutes < 4) Garantir();
                    Vigiar();
                }
                catch (Exception e) { P.Log("guarda: " + e.Message); }
                Thread.Sleep(45000);
            }
        }) { IsBackground = true }.Start();
    }

    // O que o Atrium escreve quando arranca sem as tuas definições.
    static bool DeFabrica(string j)
    {
        return j.Contains("\"preset\":\"liquid\"") && j.Contains("\"imeStatus\":true") && j.Contains("\"widgetsOnRight\":false");
    }

    static void Vigiar()
    {
        if (!File.Exists(Def)) return;
        string j = File.ReadAllText(Def);
        if (!DeFabrica(j))
        {
            if (!File.Exists(CopiaDef) || File.ReadAllText(CopiaDef) != j)
            {
                File.Copy(Def, CopiaDef, true);
                if (File.Exists(Wid)) File.Copy(Wid, CopiaWid, true);
            }
            return;
        }
        if (!File.Exists(CopiaDef)) return;
        P.Log("guarda: o Atrium está com as definições de fábrica — a repor a cópia boa");
        if (File.Exists(Atrium))
        {
            try { using (var p = Process.Start(Atrium, "--exit")) p.WaitForExit(8000); } catch { }
            Thread.Sleep(2000);
            foreach (var p in Process.GetProcessesByName("Atrium")) try { p.Kill(); } catch { }
        }
        File.Copy(CopiaDef, Def, true);
        if (File.Exists(CopiaWid)) File.Copy(CopiaWid, Wid, true);
        if (File.Exists(Atrium)) Process.Start(Atrium);
        Avisar("Central", "As definições do Atrium tinham voltado às de fábrica. Já estão repostas.");
    }

    static void Garantir()
    {
        if (File.Exists(Barra) && Process.GetProcessesByName("BarraOculta").Length == 0)
        {
            P.Log("guarda: a BarraOculta não estava a correr — a arrancar");
            Process.Start(Barra);
        }
        if (File.Exists(Atrium) && Process.GetProcessesByName("Atrium").Length == 0)
        {
            P.Log("guarda: o Atrium não estava a correr — a arrancar");
            Process.Start(Atrium);
        }
    }
}
