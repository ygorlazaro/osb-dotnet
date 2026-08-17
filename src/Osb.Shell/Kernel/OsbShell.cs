namespace Osb.Shell.Kernel;

public class HistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Main command interpreter loop for OSB.
/// </summary>
public partial class OsbShell
{
    private readonly OsbEnvironment _env;
    private readonly List<HistoryEntry> _history = [];
    private int _historyIndex;
    private bool _running = true;
    private bool _isAuthenticated;
    private string _currentUsername = string.Empty;
    private readonly bool _debugMode;

    private const int MaxHistoryEntries = 1000;
    private string HistoryFile => Path.Combine(_env.HomeDir, "CONF", "HISTORY", $"{_currentUsername}.txt");

    public OsbShell(OsbEnvironment env, bool debugMode = false)
    {
        _env = env;
        _debugMode = debugMode;
        if (_debugMode)
        {
            _isAuthenticated = true;
            _currentUsername = "ygor";
            _env.SetCurrentUsername("ygor");
        }
    }

    public void Run()
    {
        Console.Clear();
        DrawStatusBar();
        LoadHistory();
        while (_running)
        {
            DrawStatusBar();
            var input = ReadCommandLine();
            Execute(input);
        }
    }
}
