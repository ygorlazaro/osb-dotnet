namespace Osb.Shell.Kernel;

public static class ConfigUtility
{
    public static void Run(OsbEnvironment env)
    {
        var cfg = env.Config;
        var prompt = env.Prompt;
        Console.Clear();
        Console.WriteLine("*** Configuração do OSB ***");
        Console.WriteLine();
        Console.WriteLine($"1 - Mensagem de boot: {cfg.Message}");
        Console.WriteLine($"2 - Layout do prompt: {prompt.Layout}");
        Console.WriteLine("0 - Sair sem alterar");
        Console.WriteLine();
        Console.Write("Escolha uma opção para alterar: ");
        var choice = (Console.ReadLine() ?? "").Trim();

        switch (choice)
        {
            case "1":
                Console.Write("Nova mensagem de boot: ");
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
        }

        cfg.Save(env.ConfigFile);
        prompt.Save(env.HomeDir);
        Console.Clear();
    }

    private static string? EditPromptLayout(string current)
    {
        Console.WriteLine();
        Console.WriteLine("Marcadores disponíveis:");
        Console.WriteLine("  %user     = nome do usuário autenticado (ou guest)");
        Console.WriteLine("  %hostname = nome da máquina");
        Console.WriteLine("  %pwd      = diretório atual");
        Console.WriteLine("  %d        = data atual (dd/MM/yyyy)");
        Console.WriteLine("  %t        = hora atual (HH:mm:ss)");
        Console.WriteLine("  %br       = quebra de linha");
        Console.WriteLine();
        Console.Write("Novo layout (ESC para cancelar, ENTER para confirmar): ");

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
