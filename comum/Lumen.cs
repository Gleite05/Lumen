// Lumen — base comum: pastas, configuração (lumen.json), idioma e registo.
// Compilado dentro de cada programa do Lumen (Central, Painel, Clássico).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

static class Lumen
{
    // A versão vem do ficheiro VERSION (a compilação gera o AssemblyVersion).
    public static readonly string Versao = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    public const string Repo = "Gleite05/Lumen";

    // Onde está instalado (pasta dos executáveis) e onde guarda os dados do utilizador.
    public static readonly string Pasta = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
    public static readonly string Dados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen");
    public static readonly string Config = Path.Combine(Dados, "lumen.json");
    public static readonly string DefBarra = Path.Combine(Dados, "settings.json");

    public static string Exe(string nome) { return Path.Combine(Pasta, nome); }
    public static string Barra { get { return Exe("Lumen.exe"); } }
    public static string DefinicoesBarra { get { return Path.Combine(Pasta, @"Settings\Lumen.Settings.exe"); } }
    public static string Central { get { return Exe("Lumen.Central.exe"); } }
    public static string Painel { get { return Exe("Lumen.Painel.exe"); } }
    public static string Classico { get { return Exe("Lumen.Classico.exe"); } }

    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    // ------------------------------------------------------------ configuração
    public static Dictionary<string, object> Ler()
    {
        var d = Predefinicoes();
        try
        {
            if (File.Exists(Config))
            {
                var lido = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Config, Encoding.UTF8));
                if (lido != null) foreach (var kv in lido) d[kv.Key] = kv.Value;
            }
        }
        catch (Exception e) { Log("lumen", "configuração ilegível: " + e.Message); }
        return d;
    }

    public static void Gravar(Dictionary<string, object> cfg)
    {
        Directory.CreateDirectory(Dados);
        string tmp = Config + ".tmp";
        File.WriteAllText(tmp, Bonito(Json.Serialize(cfg)), new UTF8Encoding(false));
        if (File.Exists(Config)) File.Replace(tmp, Config, null); else File.Move(tmp, Config);
    }

    // O que um Lumen acabado de instalar usa.
    public static Dictionary<string, object> Predefinicoes()
    {
        return new Dictionary<string, object> {
            { "idioma", "auto" },              // auto | pt-PT | en | es
            { "tema", "esmeralda" },           // esmeralda | oceano | indigo | ambar | grafite | glaciar
            { "fundo", "vivo" },               // vivo (conta as horas) | tema (imagem do tema) | meu (o teu)
            { "cursores", "escuro" },          // escuro | claro | windows
            { "barraOculta", true },           // esconde a barra de tarefas do Windows (Ctrl+Alt+B mostra-a)
            { "paineis", new Dictionary<string, object> { { "data", true }, { "servidores", true }, { "hoje", true }, { "rede", true }, { "atalhos", true }, { "musica", true } } },
            { "horario", new ArrayList { new ArrayList { "09:00", "13:00" }, new ArrayList { "14:00", "18:00" } } },
            { "dias", new ArrayList { 1, 2, 3, 4, 5 } },
            { "feriados", new ArrayList() },
            { "servidores", new ArrayList() },
            { "atalhos", new ArrayList {
                new Dictionary<string, object> { { "nome", "Definições" }, { "icone", "gear" }, { "acao", "url:ms-settings:" } },
                new Dictionary<string, object> { { "nome", "Ficheiros" }, { "icone", "folder" }, { "acao", "exe:explorer.exe" } },
                new Dictionary<string, object> { { "nome", "Terminal" }, { "icone", "terminal" }, { "acao", "exe:wt.exe" } } } },
            { "area", new Dictionary<string, object> { { "x", 110 }, { "y", 40 }, { "w", 1400 }, { "h", 1070 } } },
            { "fecharNoClassico", new ArrayList() }, // outros programas teus que o modo clássico também fecha
        };
    }

    public static object C(Dictionary<string, object> cfg, string k) { object v; return cfg != null && cfg.TryGetValue(k, out v) ? v : null; }
    public static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
    public static bool B(Dictionary<string, object> d, string k, bool padrao) { object v; return d != null && d.TryGetValue(k, out v) && v is bool ? (bool)v : padrao; }
    public static IEnumerable<Dictionary<string, object>> Lista(Dictionary<string, object> cfg, string k)
    {
        var l = C(cfg, k) as ArrayList;
        if (l == null) yield break;
        foreach (var o in l) { var d = o as Dictionary<string, object>; if (d != null) yield return d; }
    }

    // ------------------------------------------------------------ idioma
    // pt | en | es — o que o utilizador escolheu, ou o do Windows.
    public static string Lingua = "pt";
    public static void DefinirIdioma(Dictionary<string, object> cfg)
    {
        string i = S(cfg, "idioma").ToLowerInvariant();
        if (i == "" || i == "auto") i = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Lingua = i.StartsWith("pt") ? "pt" : i.StartsWith("es") ? "es" : "en";
    }
    public static string T(string pt, string en, string es) { return Lingua == "pt" ? pt : Lingua == "es" ? es : en; }
    public static string TagBarra { get { return Lingua == "pt" ? "pt-PT" : Lingua; } } // o idioma no settings.json da barra

    // ------------------------------------------------------------ registo
    static readonly object trava = new object();
    public static void Log(string ficheiro, string s)
    {
        lock (trava)
            try
            {
                string pasta = Path.Combine(Dados, "logs");
                Directory.CreateDirectory(pasta);
                string f = Path.Combine(pasta, ficheiro + ".log");
                if (File.Exists(f) && new FileInfo(f).Length > 2 * 1024 * 1024) File.Delete(f);
                File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + s + "\r\n");
            }
            catch { }
    }

    // JSON com indentação (para o lumen.json ser legível e editável à mão).
    public static string Bonito(string json)
    {
        var sb = new StringBuilder();
        int nivel = 0; bool aspas = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\' || (i > 1 && json[i - 2] == '\\'))) aspas = !aspas;
            if (aspas) { sb.Append(c); continue; }
            if (c == '{' || c == '[')
            {
                char fecho = c == '{' ? '}' : ']';
                if (i + 1 < json.Length && json[i + 1] == fecho) { sb.Append(c).Append(fecho); i++; continue; }
                sb.Append(c).Append("\r\n").Append(new string(' ', ++nivel * 2));
            }
            else if (c == '}' || c == ']') sb.Append("\r\n").Append(new string(' ', --nivel * 2)).Append(c);
            else if (c == ',') sb.Append(c).Append("\r\n").Append(new string(' ', nivel * 2));
            else if (c == ':') sb.Append(": ");
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
