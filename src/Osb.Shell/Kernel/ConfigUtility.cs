namespace Osb.Shell.Kernel;

/// <summary>Porte simplificado do CONFIG.COM: editor das opções gerais do OSB.CFG.</summary>
public static class ConfigUtility
{
    public static void Run(OsbEnvironment env)
    {
        var cfg = env.Config;
        Console.Clear();
        Console.WriteLine("*** Configuração do OSB ***");
        Console.WriteLine();
        Console.WriteLine($"1 - Mensagem de boot: {cfg.Message}");
        Console.WriteLine($"2 - Exibir logo no boot: {(cfg.Logo ? "Sim" : "Não")}");
        Console.WriteLine($"3 - Ativar NumLock no boot: {(cfg.Num ? "Sim" : "Não")}");
        Console.WriteLine("0 - Sair sem alterar");
        Console.WriteLine();
        Console.Write("Escolha uma opção para alterar: ");
        var choice = (Console.ReadLine() ?? "").Trim();

        switch (choice)
        {
            case "1":
                Console.Write("Nova mensagem de boot: ");
                var msg = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(msg)) cfg.Message = msg;
                break;
            case "2":
                cfg.Logo = !cfg.Logo;
                break;
            case "3":
                cfg.Num = !cfg.Num;
                break;
        }

        cfg.Save(env.ConfigFile);
        Console.Clear();
    }
}
