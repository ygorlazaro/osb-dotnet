using System.IO;
using System.Linq;

namespace Osb.Shell.Kernel;

/// <summary>
/// Provedor de autocompletar (TAB) para o OSB Shell.
/// Suporta autocompletar comandos do sistema, subcomandos/parâmetros
/// (APLIC, GAMES, USER, HELP) e arquivos/diretórios do sistema de arquivos.
/// </summary>
public class TabCompleter
{
    public static readonly string[] CommandVerbs =
    [
        "ABOUT", "APLIC", "CAL", "CD", "CLEAR", "CLS", "COLOR", "CONFIG", "COPY",
        "DATE", "DIR", "ERASE", "EXIT", "GAMES", "HELP", "HISTORY", "HOSTNAME",
        "KISS", "MD", "PRINT", "PWD", "RD", "REN", "RPT", "SIZE", "TIME",
        "TREE", "TYPE", "USER", "VER", "X"
    ];

    private static readonly string[] UserSubcommands = ["ADD", "CHANGE", "DEL", "LIST"];

    private readonly OsbEnvironment _env;

    public TabCompleter(OsbEnvironment env)
    {
        _env = env;
    }

    public record ParsedContext(
        string Verb,
        List<string> Args,
        string CurrentToken,
        int TokenStart,
        int WordIndex
    );

    /// <summary>
    /// Analisa a linha de comando e a posição do cursor para identificar o token sendo editado,
    /// o verbo do comando e o índice da palavra.
    /// </summary>
    public static ParsedContext ParseContext(string lineText, int cursor)
    {
        var prefixText = cursor <= lineText.Length ? lineText[..cursor] : lineText;
        var lastSemi = prefixText.LastIndexOf(';');
        var segmentStart = lastSemi >= 0 ? lastSemi + 1 : 0;
        var segmentText = prefixText[segmentStart..];

        var tokenStartInSegment = 0;
        var inQuotes = false;

        for (var i = segmentText.Length - 1; i >= 0; i--)
        {
            var c = segmentText[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                tokenStartInSegment = i + 1;
                break;
            }
        }

        var tokenStart = segmentStart + tokenStartInSegment;
        var currentToken = segmentText[tokenStartInSegment..];

        var leadingText = segmentText[..tokenStartInSegment].Trim();
        var words = leadingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var verb = words.Length > 0 ? words[0].ToUpperInvariant() : "";
        var args = words.Skip(1).ToList();
        var wordIndex = words.Length;

        return new ParsedContext(verb, args, currentToken, tokenStart, wordIndex);
    }

    /// <summary>
    /// Retorna a lista de candidatos a autocompletar para a linha e posição de cursor informadas.
    /// </summary>
    public IReadOnlyList<string> GetCandidates(string lineText, int cursor)
    {
        var context = ParseContext(lineText, cursor);
        var currentToken = context.CurrentToken;
        var cleanToken = currentToken.TrimStart('"');
        var isQuoted = currentToken.StartsWith('"');

        var candidates = new List<string>();

        if (context.WordIndex == 0)
        {
            // Completando o comando principal (verbo)
            foreach (var cmd in CommandVerbs)
            {
                if (cmd.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(cmd + " ");
                }
            }

            // Se o token parece ser um caminho (ex: ./programa ou arquivo no dir atual), também busca no sistema de arquivos
            if (cleanToken.Contains('/') || cleanToken.Contains('\\') || cleanToken.StartsWith('.'))
            {
                candidates.AddRange(GetFileSystemCandidates(cleanToken, isQuoted, directoriesOnly: false));
            }
        }
        else
        {
            // Completando argumentos de acordo com o verbo do comando
            switch (context.Verb)
            {
                case "APLIC":
                    var appCfg = Path.Combine(_env.ConfDir, "APLIC.CFG");
                    var apps = ConfigFileParser.LoadEntries(appCfg);
                    foreach (var app in apps)
                    {
                        if (app.Name.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(app.Name + " ");
                        }
                    }
                    break;

                case "GAMES":
                    var gameCfg = Path.Combine(_env.ConfDir, "GAMES.CFG");
                    var games = ConfigFileParser.LoadEntries(gameCfg);
                    foreach (var game in games)
                    {
                        if (game.Name.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(game.Name + " ");
                        }
                    }
                    break;

                case "USER":
                    if (context.WordIndex == 1)
                    {
                        foreach (var sub in UserSubcommands)
                        {
                            if (sub.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                            {
                                candidates.Add(sub + " ");
                            }
                        }
                    }
                    else if (context.WordIndex == 2 && context.Args.Count > 0 &&
                             (context.Args[0].Equals("DEL", StringComparison.OrdinalIgnoreCase) ||
                              context.Args[0].Equals("CHANGE", StringComparison.OrdinalIgnoreCase)))
                    {
                        foreach (var u in _env.Users.Usernames)
                        {
                            if (u.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                            {
                                candidates.Add(u + " ");
                            }
                        }
                    }
                    break;

                case "HELP":
                    foreach (var cmd in CommandVerbs)
                    {
                        if (cmd.StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(cmd + " ");
                        }
                    }
                    break;

                case "CD":
                case "RD":
                case "MD":
                case "TREE":
                    candidates.AddRange(GetFileSystemCandidates(cleanToken, isQuoted, directoriesOnly: true));
                    break;

                case "DIR":
                    if ("/W".StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase) &&
                        !cleanToken.Equals("/W", StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add("/W ");
                    }
                    if ("-W".StartsWith(cleanToken, StringComparison.OrdinalIgnoreCase) &&
                        !cleanToken.Equals("-W", StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add("-W ");
                    }
                    candidates.AddRange(GetFileSystemCandidates(cleanToken, isQuoted, directoriesOnly: false));
                    break;

                case "TYPE":
                case "PRINT":
                case "SIZE":
                case "KISS":
                case "ERASE":
                case "COPY":
                case "REN":
                default:
                    candidates.AddRange(GetFileSystemCandidates(cleanToken, isQuoted, directoriesOnly: false));
                    break;
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Calcula o maior prefixo comum entre uma lista de candidatos.
    /// </summary>
    public static string GetLongestCommonPrefix(IReadOnlyList<string> strings)
    {
        if (strings.Count == 0) return "";
        if (strings.Count == 1) return strings[0];

        var first = strings[0];
        var lcpLength = first.Length;

        for (var i = 1; i < strings.Count; i++)
        {
            var current = strings[i];
            var j = 0;
            while (j < lcpLength && j < current.Length &&
                   char.ToUpperInvariant(first[j]) == char.ToUpperInvariant(current[j]))
            {
                j++;
            }
            lcpLength = j;
            if (lcpLength == 0) break;
        }

        return first[..lcpLength];
    }

    private static List<string> GetFileSystemCandidates(string token, bool isQuoted, bool directoriesOnly)
    {
        var list = new List<string>();

        var lastSlash = token.LastIndexOfAny(['/', '\\']);
        string dirPart;
        string namePrefix;
        char sep;

        if (lastSlash >= 0)
        {
            dirPart = token[..(lastSlash + 1)];
            namePrefix = token[(lastSlash + 1)..];
            sep = token[lastSlash];
        }
        else
        {
            dirPart = "";
            namePrefix = token;
            sep = '/';
        }

        string searchDir;
        try
        {
            searchDir = string.IsNullOrEmpty(dirPart)
                ? Directory.GetCurrentDirectory()
                : PathResolver.Resolve(dirPart);
        }
        catch
        {
            return list;
        }

        if (!Directory.Exists(searchDir))
        {
            return list;
        }

        try
        {
            // Atalhos especiais para . e ..
            if (".".StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) && namePrefix.Length > 0)
            {
                list.Add(FormatCandidate(dirPart, ".", true, sep, isQuoted));
            }
            if ("..".StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) && namePrefix.Length > 0)
            {
                list.Add(FormatCandidate(dirPart, "..", true, sep, isQuoted));
            }

            var dirInfo = new DirectoryInfo(searchDir);

            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                if (subDir.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(FormatCandidate(dirPart, subDir.Name, true, sep, isQuoted));
                }
            }

            if (!directoriesOnly)
            {
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    if (file.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(FormatCandidate(dirPart, file.Name, false, sep, isQuoted));
                    }
                }
            }
        }
        catch
        {
            // Ignora erros de permissão/acesso durante o autocompletar
        }

        return list;
    }

    private static string FormatCandidate(string dirPart, string name, bool isDirectory, char sep, bool isQuoted)
    {
        var path = dirPart + name + (isDirectory ? sep.ToString() : "");
        var needsQuotes = isQuoted || path.Contains(' ');

        if (needsQuotes)
        {
            return isDirectory ? $"\"{path}\"" : $"\"{path}\" ";
        }

        return isDirectory ? path : path + " ";
    }
}
