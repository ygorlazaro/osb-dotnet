namespace Osb.Shell.Kernel;

/// <summary>
/// Help system for OSB commands. Loads help text from OSB.HLP file
/// and provides categorized command listings.
/// </summary>
public static class HelpTexts
{
    private static readonly Dictionary<string, string> Texts = LoadHelpFile();

    private static string HelpFilePath
    {
        get
        {
            var fileName = I18nService.CurrentLanguage == "EN-US" ? "EN-US.HLP" : "OSB.HLP";
            return Path.Combine(AppContext.BaseDirectory, "CONF", fileName);
        }
    }

    private static Dictionary<string, string> LoadHelpFile()
    {
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(HelpFilePath))
        {
            return texts;
        }

        string? currentKey = null;
        var builder = new System.Text.StringBuilder();

        foreach (var line in File.ReadAllLines(HelpFilePath))
        {
            var rawLine = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (currentKey != null && builder.Length > 0)
                {
                    builder.AppendLine();
                }

                continue;
            }

            if (rawLine.StartsWith("-ENDCOMMAND", StringComparison.OrdinalIgnoreCase))
            {
                if (currentKey != null)
                {
                    texts[currentKey] = builder.ToString().TrimEnd();
                    currentKey = null;
                    builder.Clear();
                }
                continue;
            }

            if (rawLine.StartsWith("-ENDOFFILE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (rawLine.StartsWith("-"))
            {
                if (currentKey != null)
                {
                    texts[currentKey] = builder.ToString().TrimEnd();
                    builder.Clear();
                }

                currentKey = rawLine[1..].Trim().ToUpperInvariant();
                continue;
            }

            if (currentKey == null)
            {
                continue;
            }

            builder.AppendLine(rawLine);
        }

        if (currentKey != null)
        {
            texts[currentKey] = builder.ToString().TrimEnd();
        }

        return texts;
    }

    private static readonly (string Titulo, string[] Comandos)[] Categorias =
    [
        (I18nService.Get("help.category.files"), ["DIR", "CD", "MD", "RD", "COPY", "DEL", "REN", "MOVE", "FIND", "TYPE", "SIZE", "PWD", "PRINT", "RECOVER"
        ]),
        (I18nService.Get("help.category.system"), ["CLS", "COLOR", "CONFIG", "DATE", "TIME", "VER", "ABOUT", "EXIT", "HELP", "HISTORY", "HOSTNAME", "USER", "PROMPT", "SET"
        ]),
        (I18nService.Get("help.category.apps"), ["CAL", "KISS", "TOUR", "TODO", "HANGMAN", "OSL"]),
        (I18nService.Get("help.category.external"), ["."])
    ];

    /// <summary>O que HELP (sem argumento) mostra: a lista de comandos por categoria, mais dicas.</summary>
    private static string BuildOverview()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(I18nService.Get("commands.available_commands"));
        sb.AppendLine();

        foreach (var (title, commands) in Categorias)
        {
            sb.AppendLine($"[{title}]");
            foreach (var cmd in commands)
            {
                var summary = Texts.TryGetValue(cmd, out var text) ? text.Split('\n')[0] : string.Empty;
                sb.AppendLine($"  {cmd,-8}{summary}");
            }
            sb.AppendLine();
        }

        sb.AppendLine(I18nService.Get("commands.tips"));
        sb.AppendLine(I18nService.Get("help.tip.detailed"));
        sb.AppendLine(I18nService.Get("help.tip.detailed_example"));
        sb.AppendLine(I18nService.Get("help.tip.tab"));
        sb.AppendLine(I18nService.Get("help.tip.arrows"));
        sb.AppendLine(I18nService.Get("help.tip.history"));
        sb.AppendLine(I18nService.Get("help.tip.chain"));
        sb.AppendLine(I18nService.Get("help.tip.history_cmd"));
        return sb.ToString();
    }

    public static void Show(string command)
    {
        command = command.Trim().ToUpperInvariant();
        if (command is "" or "HELP")
        {
            Console.WriteLine(BuildOverview());
            return;
        }

        if (Texts.TryGetValue(command, out var text))
        {
            Console.WriteLine(text);
        }
        else
        {
            Console.WriteLine(I18nService.Get("commands.help_not_found", command));
        }
    }
}
