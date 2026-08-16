namespace Osb.Shell.Kernel;

/// <summary>
/// OSB environment configuration and paths.
/// Uses the current working directory as the base for OSB data.
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
    public string CurrentLanguage => Users.GetLanguage(CurrentUsername);
    public string MachineName { get; private set; } = "OSB";
    public UserManager Users { get; private set; } = null!;
    public bool DebugMode { get; }

    public OsbEnvironment(bool debugMode = false)
    {
        DebugMode = debugMode;
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

        if (debugMode)
        {
            EnsureDebugUser();
        }
    }

    private void EnsureDebugUser()
    {
        if (!Users.Exists("ygor"))
        {
            Users.Add("ygor", "debug", "EN-US", out _);
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
        WriteIfMissing(Path.Combine(ConfDir, "PROMPT.CFG"), PromptConfig.DefaultLayout + Environment.NewLine);

        WriteIfMissing(Path.Combine(ConfDir, "GAMES.CFG"),
            "HANGMAN\nJogo da forca\n");
        WriteIfMissing(Path.Combine(ConfDir, "GAMES.EN-US.CFG"),
            "HANGMAN\nHangman game\n");

        var aplicPath = Path.Combine(ConfDir, "APLIC.CFG");
        if (!File.Exists(aplicPath))
        {
            File.WriteAllText(aplicPath,
                "CAL\nCalendário em tempo real\nKISS\nEditor de texto simples\nTOUR\nTour passo a passo do OSB\nTODO\nGerenciador de tarefas\n");
        }
        WriteIfMissing(Path.Combine(ConfDir, "APLIC.EN-US.CFG"),
            "CAL\nReal-time calendar\nKISS\nSimple text editor\nTOUR\nOSB step-by-step tour\nTODO\nTask manager\n");

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
        Console.WriteLine(I18nService.Get("boot.first_time"));
        Console.Write(I18nService.Get("boot.machine_name_prompt"));
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
            Console.Write(I18nService.Get("boot.initial_user_prompt"));
            username = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine(I18nService.Get("boot.username_required"));
            }
        } while (string.IsNullOrWhiteSpace(username));

        string password;
        while (true)
        {
            password = PromptForPassword(I18nService.Get("boot.password_prompt"));
            var confirm = PromptForPassword(I18nService.Get("boot.confirm_password_prompt"));
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(I18nService.Get("boot.password_required"));
                continue;
            }

            if (password != confirm)
            {
                Console.WriteLine(I18nService.Get("boot.password_mismatch"));
                continue;
            }

            break;
        }

        Users.Add(username, password, "PT-BR", out _);
        Console.WriteLine(I18nService.Get("boot.user_created", username));
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
        if (!string.IsNullOrWhiteSpace(username))
        {
            I18nService.SetLanguage(CurrentLanguage);
            Environment.SetEnvironmentVariable("LANGUAGE", CurrentLanguage);
        }
    }
}
