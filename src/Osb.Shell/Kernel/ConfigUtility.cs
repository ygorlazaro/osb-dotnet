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
                Console.Write("Novo layout do prompt: ");
                var layout = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(layout))
                {
                    prompt.Layout = layout;
                }
                break;
        }

        cfg.Save(env.ConfigFile);
        prompt.Save(env.HomeDir);
        Console.Clear();
    }
}
