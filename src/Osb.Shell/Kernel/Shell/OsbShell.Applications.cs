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
            Console.WriteLine(I18nService.Get("apps.must_be_authenticated_hostname"));
            return;
        }

        _env.SaveMachineName(args.Trim());
        Console.WriteLine(I18nService.Get("apps.machine_name_set", _env.MachineName));
    }

    private void RunAplic(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "APLIC.CFG");
        var apps = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine(I18nService.Get("apps.installed_apps"));
            foreach (var app in apps)
                Console.WriteLine($"{app.Name} - {app.Description}");
            Console.WriteLine();
            Console.WriteLine(I18nService.Get("apps.use_app"));
            return;
        }

        var entry = apps.FirstOrDefault(a => a.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine(I18nService.Get("apps.app_not_found", arg));
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "CAL": RunOslFile("APLIC/CAL/main.osl"); break;
            case "KISS": TextEditor.Run("", _env); break;
            case "TOUR": RunOslFile("APLIC/TOUR/main.osl"); break;
            case "TODO": RunOslFile("APLIC/TODO/main.osl"); break;
            default: Console.WriteLine(I18nService.Get("apps.app_not_available", entry.Name)); break;
        }
    }

    private void RunGames(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "GAMES.CFG");
        var games = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine(I18nService.Get("apps.installed_games"));
            foreach (var game in games)
                Console.WriteLine($"{game.Name} - {game.Description}");
            Console.WriteLine();
            Console.Write(I18nService.Get("apps.enter_choice"));
            arg = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (arg == "")
            {
                return;
            }
        }

        var entry = games.FirstOrDefault(g => g.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine(I18nService.Get("apps.game_not_found", arg));
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "HANGMAN": RunOslFile("Games/Hangman/main.osl"); break;
            default: Console.WriteLine(I18nService.Get("apps.game_not_available", entry.Name)); break;
        }
    }
}
