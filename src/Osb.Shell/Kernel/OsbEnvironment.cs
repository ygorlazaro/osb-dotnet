namespace Osb.Shell.Kernel;

/// <summary>
/// OSB environment configuration and paths.
/// Uses ~/.osb as the home directory with a CONF subfolder for configuration files.
/// </summary>
public class OsbEnvironment
{
    public string HomeDir { get; }
    public string ConfDir => Path.Combine(HomeDir, "CONF");
    public string ConfigFile => Path.Combine(HomeDir, "OSB.CFG");
    public string HostnameFile => Path.Combine(ConfDir, "HOSTNAME.CFG");
    public string UserConfigFile => Path.Combine(ConfDir, "USER.CFG");
    public OsbConfig Config { get; private set; } = null!;
    public PromptConfig Prompt { get; private set; } = null!;
    public VariableStore Variables { get; private set; } = null!;
    public string CurrentUsername { get; private set; } = string.Empty;
    public string MachineName { get; private set; } = "OSB";
    public UserManager Users { get; private set; } = null!;

    public OsbEnvironment()
    {
        // ~/.osb é compartilhado com o Osb.Xwin (que lê o mesmo OSB.CFG/CONF de lá) -
        // por isso não pode ser a pasta de build de cada executável (AppContext.BaseDirectory),
        // já que Shell e Xwin são processos/pastas de build diferentes; usar a pasta de build
        // também faria o histórico/usuários serem apagados a cada "dotnet build" limpo.
        HomeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osb");
        var firstBoot = EnsureInstalled();
        Config = OsbConfig.Load(ConfigFile);
        Prompt = PromptConfig.Load(HomeDir);
        Variables = new VariableStore(HomeDir);
        MachineName = LoadMachineName();
        Users = new UserManager(UserConfigFile);

        if (firstBoot)
        {
            PerformFirstBootSetup();
        }
    }

    private bool EnsureInstalled()
    {
        Directory.CreateDirectory(HomeDir);
        Directory.CreateDirectory(ConfDir);

        if (!File.Exists(ConfigFile))
        {
            var cfg = new OsbConfig { SystemDir = HomeDir };
            cfg.Save(ConfigFile);
        }

        WriteIfMissing(Path.Combine(ConfDir, "START.CFG"), "VER\n");
        WriteIfMissing(Path.Combine(ConfDir, "SYSTEM.CFG"), "[CLOCK]\nTRUE\n\n[MOUSE]\nFALSE\n");
        WriteIfMissing(Path.Combine(ConfDir, "GAMES.CFG"),
            "HANGMAN\nJogo da forca\n");
        WriteIfMissing(Path.Combine(ConfDir, "XWIN.CFG"),
            "XWIN_TEXT\nXWinText - Editor de texto paralelo\n");
        WriteIfMissing(Path.Combine(ConfDir, "PROMPT.CFG"), PromptConfig.DefaultLayout + Environment.NewLine);

        var aplicPath = Path.Combine(ConfDir, "APLIC.CFG");
        if (!File.Exists(aplicPath))
        {
            File.WriteAllText(aplicPath,
                "CAL\nCalendário em tempo real\nKISS\nEditor de texto simples\nTOUR\nTour passo a passo do OSB\n");
        }
        else
        {
            var content = File.ReadAllText(aplicPath);
            if (!content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.Equals("TOUR", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(aplicPath, "TOUR\nTour passo a passo do OSB\n");
            }
        }

        var hostnameMissing = !File.Exists(HostnameFile) || string.IsNullOrWhiteSpace(File.ReadAllText(HostnameFile));
        var userConfigMissing = !File.Exists(UserConfigFile) || string.IsNullOrWhiteSpace(File.ReadAllText(UserConfigFile));
        return hostnameMissing || userConfigMissing;
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
        }
    }

    public void ApplyColors()
    {
        Console.ForegroundColor = DosColors.ToConsoleColor(Config.ForeColor);
        Console.BackgroundColor = DosColors.ToConsoleColor(Config.BackColor);
    }

    private string LoadMachineName()
    {
        try
        {
            if (!File.Exists(HostnameFile))
            {
                return "OSB";
            }

            var name = File.ReadAllText(HostnameFile).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(name) ? "OSB" : name.Trim();
        }
        catch
        {
            return "OSB";
        }
    }

    public void SaveMachineName(string machineName)
    {
        var value = string.IsNullOrWhiteSpace(machineName) ? "OSB" : machineName.Trim();
        Directory.CreateDirectory(ConfDir);
        File.WriteAllText(HostnameFile, value + Environment.NewLine);
        MachineName = value;
    }

    private void PerformFirstBootSetup()
    {
        Console.WriteLine("Primeiro boot detectado. Configurando nome da máquina e usuário inicial.");
        Console.Write("Nome da máquina [OSB]: ");
        var hostName = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(hostName))
        {
            hostName = "OSB";
        }

        SaveMachineName(hostName);

        Console.WriteLine();
        string username;
        do
        {
            Console.Write("Usuário inicial: ");
            username = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("O nome do usuário não pode ficar vazio.");
            }
        } while (string.IsNullOrWhiteSpace(username));

        string password;
        while (true)
        {
            password = PromptForPassword("Senha inicial: ");
            var confirm = PromptForPassword("Confirme a senha: ");
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("A senha não pode ficar vazia.");
                continue;
            }

            if (password != confirm)
            {
                Console.WriteLine("As senhas não conferem. Tente novamente.");
                continue;
            }

            break;
        }

        // Importante: passa pelo Users.Add (não escreve o arquivo direto) - o UserManager
        // já foi construído nesse ponto com a lista carregada do disco (vazia, no primeiro
        // boot). Escrever o arquivo por fora deixava esse cache em memória desatualizado,
        // e o primeiro login sempre dava "senha incorreta" mesmo com a senha certa.
        Users.Add(username, password, out _);
        Console.WriteLine($"Usuário inicial '{username}' criado com sucesso.");
        Console.WriteLine();
    }

    private static string PromptForPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Count > 0)
                {
                    password.RemoveAt(password.Count - 1);
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Add(key.KeyChar);
            }
        }

        return new string(password.ToArray());
    }

    public void ReloadColorsFromDisk()
    {
        Config = OsbConfig.Load(ConfigFile);
        ApplyColors();
    }

    public void SetCurrentUsername(string username)
    {
        CurrentUsername = username;
    }
}
