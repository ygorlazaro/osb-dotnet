using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Osb.Shell.Kernel;

public class VariableStore
{
    private readonly string _basePath;
    private readonly Dictionary<string, Dictionary<string, string>> _userVariables;

    public VariableStore(string homeDir)
    {
        _basePath = Path.Combine(homeDir, "CONF", "VARS");
        _userVariables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> GetForUser(string username)
    {
        if (!_userVariables.TryGetValue(username, out var vars))
        {
            vars = LoadUserVariables(username);
            _userVariables[username] = vars;
        }
        return vars;
    }

    public bool TryGetValue(string username, string name, out string? value)
    {
        if (GetForUser(username).TryGetValue(name, out var v))
        {
            value = v;
            return true;
        }
        value = null;
        return false;
    }

    public void Set(string username, string name, string value)
    {
        if (!_userVariables.TryGetValue(username, out var vars))
        {
            vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _userVariables[username] = vars;
        }
        
        if (string.IsNullOrEmpty(value))
        {
            vars.Remove(name);
        }
        else
        {
            vars[name] = value;
        }
        
        Save(username);
    }

    public void Remove(string username, string name)
    {
        if (_userVariables.TryGetValue(username, out var vars))
        {
            vars.Remove(name);
            Save(username);
        }
    }

    public void Save(string homeDir, string username)
    {
        var path = GetUserFilePath(homeDir, username);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (_userVariables.TryGetValue(username, out var vars))
            {
                var lines = vars.Select(kv => $"{kv.Key}={kv.Value}");
                File.WriteAllLines(path, lines);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void Save(string username)
    {
        Save(_basePath, username);
    }

    private Dictionary<string, string> LoadUserVariables(string username)
    {
        var path = GetUserFilePath(_basePath, username);
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                    {
                        continue;
                    }
                    
                    var equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        var name = trimmed[..equalsIndex].Trim();
                        var value = trimmed[(equalsIndex + 1)..].Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            vars[name] = value;
                        }
                    }
                }
            }
        }
        catch
        {
        }
        
        return vars;
    }

    private static string GetUserFilePath(string basePath, string username)
    {
        return Path.Combine(basePath, $"{username}.vars");
    }
}
