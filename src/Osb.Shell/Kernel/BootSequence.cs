namespace Osb.Shell.Kernel;

public static class BootSequence
{
    public static void Run(OsbEnvironment env, OsbShell shell)
    {
        Console.Clear();
        Console.WriteLine(I18nService.Get("boot.starting"));
        Console.WriteLine(I18nService.Get("boot.reading_config", env.ConfigFile));
        Console.WriteLine(I18nService.Get("boot.setting_colors"));
        env.ApplyColors();

        Console.WriteLine(I18nService.Get("boot.setting_dirs"));
        Console.WriteLine(I18nService.Get("boot.osb_dir", env.HomeDir));

        Thread.Sleep(300);
        Console.Clear();
        Console.WriteLine(I18nService.Get("boot.date", DateTime.Now.ToString("dd/MM/yyyy")));
        Console.WriteLine(I18nService.Get("boot.time", DateTime.Now.ToString("HH:mm:ss")));
        Console.WriteLine();
        Console.WriteLine(env.Config.Message);
        Console.WriteLine();

        if (!env.DebugMode)
        {
            shell.RequireLogin();
        }

        RunStartupCommands(env, shell);
    }

    private static void RunStartupCommands(OsbEnvironment env, OsbShell shell)
    {
        var startFile = Path.Combine(env.ConfDir, "START.CFG");
        if (!File.Exists(startFile))
        {
            return;
        }

        foreach (var line in File.ReadAllLines(startFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            shell.Execute(line);
        }
    }
}
