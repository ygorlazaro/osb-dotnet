using System.Linq;
using Osb.Shell.Apps;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private void HandleHostname(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine(_env.MachineName);
            return;
        }

        if (!_isAuthenticated)
        {
            Console.WriteLine("Você deve estar autenticado para alterar o nome da máquina.");
            return;
        }

        _env.SaveMachineName(args.Trim());
        Console.WriteLine("Nome da máquina definido para: " + _env.MachineName);
    }

    private void RunAplic(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "APLIC.CFG");
        var apps = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine("*** Aplicativos instalados no OSB ***");
            foreach (var app in apps)
                Console.WriteLine($"{app.Name} - {app.Description}");
            Console.WriteLine();
            Console.WriteLine("Use: APLIC <nome>  (ex: APLIC CAL)");
            return;
        }

        var entry = apps.FirstOrDefault(a => a.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine("Aplicativo não encontrado: " + arg);
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "CAL": Calendar.Show(""); break;
            case "KISS": TextEditor.Run("", _env); break;
            case "TOUR": RunOslFile("APLIC/TOUR/main.osl"); break;
            default: Console.WriteLine("Aplicativo não portado para .NET: " + entry.Name); break;
        }
    }

    private void RunGames(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "GAMES.CFG");
        var games = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine("*** Games instalados no OSB ***");
            foreach (var game in games)
                Console.WriteLine($"{game.Name} - {game.Description}");
            Console.WriteLine();
            Console.Write("Entre com sua escolha (<ENTER> para sair): ");
            arg = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (arg == "")
            {
                return;
            }
        }

        var entry = games.FirstOrDefault(g => g.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine("Jogo não encontrado: " + arg);
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "HANGMAN": RunOslFile("Games/Hangman/main.osl"); break;
            default: Console.WriteLine("Jogo não portado para .NET: " + entry.Name); break;
        }
    }
}
