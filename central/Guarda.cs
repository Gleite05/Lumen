// Guarda: nos primeiros minutos depois de iniciar sessão garante que a barra do Lumen está a correr
// (o Windows às vezes arranca os programas antes de o ambiente de trabalho estar pronto).
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

static class Guarda
{
    public static void Iniciar()
    {
        new Thread(() =>
        {
            var fim = DateTime.Now.AddMinutes(4);
            Thread.Sleep(15000);
            while (DateTime.Now < fim)
            {
                try
                {
                    if (File.Exists(Lumen.Barra) && Process.GetProcessesByName("Lumen").Length == 0)
                    {
                        P.Log("guarda: a barra não estava a correr — a arrancar");
                        Process.Start(Lumen.Barra);
                    }
                }
                catch (Exception e) { P.Log("guarda: " + e.Message); }
                Thread.Sleep(30000);
            }
        }) { IsBackground = true }.Start();
    }
}
