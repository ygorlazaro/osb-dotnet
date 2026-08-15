namespace Osb.Shell.Kernel;

public static class ConfigUtility
{
    public static void Run(OsbEnvironment env)
    {
        var cfg = env.Config;
        var prompt = env.Prompt;
        Console.Clear();
        Console.WriteLine(I18nService.Get("config.title"));
        Console.WriteLine();
        Console.WriteLine(I18nService.Get("config.boot_message", cfg.Message));
        Console.WriteLine(I18nService.Get("config.prompt_layout", prompt.Layout));
        Console.WriteLine(I18nService.Get("config.exit"));
        Console.WriteLine();
        Console.Write(I18nService.Get("config.choice"));
        var choice = (Console.ReadLine() ?? "").Trim();

        switch (choice)
        {
            case "1":
                Console.Write(I18nService.Get("config.new_boot_message"));
                var msg = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    cfg.Message = msg;
                }
                break;
            case "2":
                var newLayout = EditPromptLayout(prompt.Layout);
                if (newLayout is not null)
                {
                    prompt.Layout = newLayout;
                }
                break;
            case "3":
                Console.WriteLine();
                Console.WriteLine(I18nService.Get("config.available_markers"));
                Console.WriteLine(I18nService.Get("config.marker_user"));
                Console.WriteLine(I18nService.Get("config.marker_hostname"));
                Console.WriteLine(I18nService.Get("config.marker_pwd"));
                Console.WriteLine(I18nService.Get("config.marker_date"));
                Console.WriteLine(I18nService.Get("config.marker_time"));
                Console.WriteLine(I18nService.Get("config.marker_br"));
                Console.WriteLine();
                Console.Write(I18nService.Get("config.new_layout_prompt"));
                var lang = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                if (lang == "PT-BR" || lang == "EN-US")
                {
                    I18nService.SetLanguage(lang);
                }
                break;
        }

        cfg.Save(env.ConfigFile);
        prompt.Save(env.HomeDir);
        Console.Clear();
    }

    private static string? EditPromptLayout(string current)
    {
        Console.WriteLine();
        Console.WriteLine(I18nService.Get("config.available_markers"));
        Console.WriteLine(I18nService.Get("config.marker_user"));
        Console.WriteLine(I18nService.Get("config.marker_hostname"));
        Console.WriteLine(I18nService.Get("config.marker_pwd"));
        Console.WriteLine(I18nService.Get("config.marker_date"));
        Console.WriteLine(I18nService.Get("config.marker_time"));
        Console.WriteLine(I18nService.Get("config.marker_br"));
        Console.WriteLine();
        Console.Write(I18nService.Get("config.new_layout_prompt"));

        var buffer = current.ToList();
        var cursor = buffer.Count;
        Console.Write(new string(buffer.ToArray()));

        while (true)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(buffer.ToArray());
            }

            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return null;
            }

            if (key.Key == ConsoleKey.LeftArrow && cursor > 0)
            {
                cursor--;
                Console.Write("\u001b[1D");
                continue;
            }

            if (key.Key == ConsoleKey.RightArrow && cursor < buffer.Count)
            {
                cursor++;
                Console.Write("\u001b[1C");
                continue;
            }

            if (key.Key == ConsoleKey.Home && cursor > 0)
            {
                Console.Write($"\u001b[{cursor}D");
                cursor = 0;
                continue;
            }

            if (key.Key == ConsoleKey.End && cursor < buffer.Count)
            {
                var n = buffer.Count - cursor;
                Console.Write($"\u001b[{n}C");
                cursor = buffer.Count;
                continue;
            }

            if (key.Key == ConsoleKey.Backspace && cursor > 0)
            {
                cursor--;
                buffer.RemoveAt(cursor);
                Console.Write("\b \b");
                var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
                Console.Write(tail + new string(' ', 1));
                Console.Write($"\u001b[{tail.Length + 1}D");
                continue;
            }

            if (key.Key == ConsoleKey.Delete && cursor < buffer.Count)
            {
                buffer.RemoveAt(cursor);
                var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
                Console.Write(tail + new string(' ', 1));
                Console.Write($"\u001b[{tail.Length + 1}D");
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Insert(cursor, key.KeyChar);
                cursor++;
                var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
                Console.Write(key.KeyChar + tail);
                Console.Write($"\u001b[{tail.Length}D");
            }
        }
    }
}
