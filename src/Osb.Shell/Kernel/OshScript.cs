using System.IO;

namespace Osb.Shell.Kernel;

/// <summary>
/// Very simple shell script runner for .osh files.
///
/// Syntax:
///   - Empty lines are ignored.
///   - Lines starting with ';', 'REM', or ''' are comments.
///   - SET VAR=value defines/replaces a variable.
///   - Positional parameters: %1%, %2%, etc. are populated from script arguments.
///   - Any other line is executed as an OSB.SHELL command.
///   - %VAR% is expanded in command lines using the current user's variable store.
/// </summary>
public class OshScript
{
    private readonly OsbEnvironment _env;
    private readonly OsbShell _shell;

    public OshScript(OsbEnvironment env, OsbShell shell)
    {
        _env = env;
        _shell = shell;
    }

    public void RunFile(string path, string[]? args = null)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Script não encontrado: {path}");
            return;
        }

        var positionalArgs = (args ?? []).Select(a => a.Trim()).ToArray();
        var previousPositional = new Dictionary<string, string?>();

        try
        {
            for (var i = 0; i < positionalArgs.Length; i++)
            {
                var key = (i + 1).ToString();
                previousPositional[key] = _env.Variables.TryGetValue(_env.CurrentUsername, key, out var existing) ? existing : null;
                _env.Variables.Set(_env.CurrentUsername, key, positionalArgs[i]);
            }

            var lines = File.ReadAllLines(path);
            var lineNumber = 0;

            foreach (var rawLine in lines)
            {
                lineNumber++;
                var line = rawLine.Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                if (IsComment(line))
                {
                    continue;
                }

                if (TryParseSet(line, out var varName, out var varValue))
                {
                    _env.Variables.Set(_env.CurrentUsername, varName, varValue);
                    continue;
                }

                var expanded = ExpandVariables(line);
                try
                {
                    _shell.Execute(expanded, requireAuth: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro na linha {lineNumber}: {ex.Message}");
                }
            }
        }
        finally
        {
            foreach (var (key, oldValue) in previousPositional)
            {
                if (oldValue is null)
                {
                    _env.Variables.Remove(_env.CurrentUsername, key);
                }
                else
                {
                    _env.Variables.Set(_env.CurrentUsername, key, oldValue);
                }
            }
        }
    }

    private static bool IsComment(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.StartsWith("REM ") || upper.StartsWith("REM\t") || upper.StartsWith("'") || upper.StartsWith(";");
    }

    private static bool TryParseSet(string line, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;

        if (!line.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var args = line[4..].Trim();
        var equalsIndex = args.IndexOf('=');
        if (equalsIndex < 0)
        {
            name = args;
            value = string.Empty;
            return true;
        }

        name = args[..equalsIndex].Trim();
        value = args[(equalsIndex + 1)..].Trim();
        return true;
    }

    private string ExpandVariables(string input)
    {
        if (string.IsNullOrEmpty(_env.CurrentUsername))
        {
            return input;
        }

        var vars = _env.Variables.GetForUser(_env.CurrentUsername);
        var result = input;
        foreach (var (name, val) in vars)
        {
            result = result.Replace($"%{name}%", val);
        }
        return result;
    }
}
