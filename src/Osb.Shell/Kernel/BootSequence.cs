namespace Osb.Shell.Kernel;

public static class BootSequence
{
    public static void Run(OsbEnvironment env, OsbShell shell)
    {
        Console.Clear();
        Console.WriteLine("Iniciando o processo de boot do OSB");
        Console.WriteLine("Lendo " + env.ConfigFile);
        Console.WriteLine("Definindo as cores");
        env.ApplyColors();

        Console.WriteLine("Definindo os diretórios");
        Console.WriteLine("Pasta do OSB: " + env.HomeDir);

        Thread.Sleep(300);
        Console.Clear();
        Console.WriteLine("Data: " + DateTime.Now.ToString("dd/MM/yyyy"));
        Console.WriteLine("Hora: " + DateTime.Now.ToString("HH:mm:ss"));
        Console.WriteLine();
        Console.WriteLine(env.Config.Message);
        Console.WriteLine();

        shell.RequireLogin();

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
