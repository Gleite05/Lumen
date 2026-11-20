// Temas do Lumen: as cores do fundo vivo (8 camadas, de baixo para cima), do vidro dos painéis e do texto.
using System.Collections.Generic;

class Tema
{
    public string Id, Pt, En, Es;
    public string[] Base, Luz;      // camada apagada / acesa, índice 0 = a de baixo
    public string Ceu1, Ceu2;       // céu (topo → base)
    public int[] Crista;            // fio de luz na crista da camada da hora em curso (RGB)
    public string Vidro, Texto, Suave; // CSS dos painéis
    public bool Claro;
    public string Nome { get { return Lumen.T(Pt, En, Es); } }

    public Dictionary<string, object> ParaPagina()
    {
        return new Dictionary<string, object> {
            { "id", Id }, { "nome", Nome }, { "base", Base }, { "luz", Luz }, { "ceu", new[] { Ceu1, Ceu2 } },
            { "vidro", Vidro }, { "texto", Texto }, { "suave", Suave }, { "claro", Claro } };
    }
}

static class Temas
{
    public static readonly Tema[] Todos = {
        new Tema { Id = "esmeralda", Pt = "Esmeralda", En = "Emerald", Es = "Esmeralda",
            Base = new[] { "#1d5236", "#1a4c32", "#17462e", "#15402a", "#133b27", "#103322", "#0d2a1c", "#0a2116" },
            Luz = new[] { "#2f8a58", "#38935f", "#429c67", "#4ea670", "#5bb07a", "#6aba85", "#7bc592", "#8fd0a1" },
            Ceu1 = "#08140e", Ceu2 = "#04100a", Crista = new[] { 201, 255, 220 },
            Vidro = "rgba(10,30,20,.40)", Texto = "#eafff2", Suave = "rgba(234,255,242,.62)" },
        new Tema { Id = "oceano", Pt = "Oceano", En = "Ocean", Es = "Océano",
            Base = new[] { "#0f4766", "#0d4160", "#0b3b58", "#0a3550", "#082f48", "#06283e", "#052134", "#041a2a" },
            Luz = new[] { "#1f7fa8", "#2489b0", "#2a93b8", "#319dc0", "#3aa7c8", "#46b1cf", "#55bcd6", "#69c8de" },
            Ceu1 = "#04121f", Ceu2 = "#020b14", Crista = new[] { 200, 240, 255 },
            Vidro = "rgba(6,22,36,.42)", Texto = "#e8f7ff", Suave = "rgba(232,247,255,.62)" },
        new Tema { Id = "indigo", Pt = "Índigo", En = "Indigo", Es = "Índigo",
            Base = new[] { "#3a2580", "#352279", "#301f70", "#2b1c66", "#26195c", "#201551", "#1a1145", "#140d38" },
            Luz = new[] { "#6a4fc0", "#7257c7", "#7b60cd", "#8569d3", "#8f73d9", "#9a7ede", "#a68ae3", "#b398e8" },
            Ceu1 = "#0d0824", Ceu2 = "#07041a", Crista = new[] { 226, 216, 255 },
            Vidro = "rgba(20,12,48,.42)", Texto = "#f0ebff", Suave = "rgba(240,235,255,.62)" },
        new Tema { Id = "ambar", Pt = "Âmbar", En = "Amber", Es = "Ámbar",
            Base = new[] { "#7a3410", "#71300f", "#682c0d", "#5f280c", "#55230a", "#4a1e08", "#3f1906", "#341405" },
            Luz = new[] { "#c0601f", "#c76924", "#ce732a", "#d47d31", "#da883a", "#df9445", "#e4a052", "#e9ad62" },
            Ceu1 = "#1c0a04", Ceu2 = "#120603", Crista = new[] { 255, 226, 196 },
            Vidro = "rgba(40,16,6,.42)", Texto = "#fff3e8", Suave = "rgba(255,243,232,.64)" },
        new Tema { Id = "grafite", Pt = "Grafite", En = "Graphite", Es = "Grafito",
            Base = new[] { "#2c323c", "#282e37", "#252a32", "#21252d", "#1d2128", "#191c22", "#15181d", "#111317" },
            Luz = new[] { "#4f5a6b", "#566275", "#5e6a7e", "#667388", "#6f7c92", "#79869c", "#8490a6", "#909cb1" },
            Ceu1 = "#0c0e12", Ceu2 = "#07080b", Crista = new[] { 225, 232, 245 },
            Vidro = "rgba(16,18,24,.45)", Texto = "#eef2f8", Suave = "rgba(238,242,248,.62)" },
        new Tema { Id = "glaciar", Pt = "Glaciar (claro)", En = "Glacier (light)", Es = "Glaciar (claro)", Claro = true,
            Base = new[] { "#b5c6d6", "#bccbda", "#c3d1de", "#cad6e3", "#d1dce7", "#d8e1eb", "#dfe7ef", "#e6ecf3" },
            Luz = new[] { "#7fa6c8", "#88adcd", "#91b4d2", "#9bbbd7", "#a6c3dc", "#b1cae1", "#bcd2e6", "#c8daeb" },
            Ceu1 = "#eef3f8", Ceu2 = "#dfe8f1", Crista = new[] { 60, 110, 160 },
            Vidro = "rgba(255,255,255,.46)", Texto = "#142233", Suave = "rgba(20,34,51,.62)" },
    };

    public static Tema Obter(string id)
    {
        foreach (var t in Todos) if (t.Id == id) return t;
        return Todos[0];
    }
}
