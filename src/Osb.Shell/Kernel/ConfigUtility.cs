namespace Osb.Shell.Kernel;

public static class ConfigUtility
{
    public static void Run(OsbEnvironment env)
    {
        var cfg = env.Config;
        Console.Clear();
        Console.WriteLine("*** Configuração do OSB ***");
        Console.WriteLine();
        Console.WriteLine($"1 - Mensagem de boot: {cfg.Message}");
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
        }

        cfg.Save(env.ConfigFile);
        Console.Clear();
    }
}
