using Osb.Shell.Apps;
using Osb.Shell.Games;

namespace Osb.Shell.Kernel;

/// <summary>
/// Porte da SUB Command(Comando$) do OSB.BAS original: o loop principal do
/// interpretador de comandos do OSB. Cada bloco IF do BASIC virou um "case"
/// aqui, mantendo a mesma ordem e o mesmo comportamento sempre que possível.
/// </summary>
public partial class OsbShell
{
    private readonly OsbEnvironment _env;
    private readonly List<string> _history = [];
    private int _historyIndex;
    private bool _running = true;
    private bool _isAuthenticated;
    private string _currentUsername = string.Empty;

    private const int MaxHistoryEntries = 1000;
    private string HistoryFile => Path.Combine(_env.HomeDir, "HISTORY.TXT");

    public OsbShell(OsbEnvironment env) => _env = env;

    public void Run()
    {
        Console.Clear();
        LoadHistory();
        PrintStatusLine();
        while (_running)
        {
            var input = ReadCommandLine();
            Execute(input);
        }
    }
}
