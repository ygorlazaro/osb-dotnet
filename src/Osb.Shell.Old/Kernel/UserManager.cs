using System.Linq;

namespace Osb.Shell.Kernel;

public sealed class UserManager
{
    private readonly string _path;
    private readonly Dictionary<string, UserEntry> _users;

    public UserManager(string path)
    {
        _path = path;
        _users = LoadUsers(path);
    }

    public IReadOnlyList<string> Usernames => _users.Values
        .Select(u => u.Name)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool Validate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return _users.TryGetValue(username.Trim(), out var entry) && entry.Password == password;
    }

    public bool Exists(string username)
    {
        return !string.IsNullOrWhiteSpace(username) && _users.ContainsKey(username.Trim());
    }

    public string GetDisplayName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return string.Empty;
        }

        return _users.TryGetValue(username.Trim(), out var entry)
            ? entry.Name
            : username.Trim();
    }

    public string GetLanguage(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "EN-US";
        }

        return _users.TryGetValue(username.Trim(), out var entry)
            ? entry.Language
            : "EN-US";
    }

    public bool Add(string username, string password, string language, out string message)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            message = "Nome de usuário obrigatório.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            message = "Senha obrigatória.";
            return false;
        }

        username = username.Trim();

        if (_users.ContainsKey(username))
        {
            message = "Usuário já existe.";
            return false;
        }

        _users[username] = new UserEntry(username, password, language ?? "EN-US");
        Save();
        message = "Usuário adicionado com sucesso.";
        return true;
    }

    public bool ChangePassword(string username, string newPassword, out string message)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            message = "Nome de usuário obrigatório.";
            return false;
        }

        if (!_users.ContainsKey(username.Trim()))
        {
            message = "Usuário não encontrado.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            message = "Senha obrigatória.";
            return false;
        }

        var key = username.Trim();
        var entry = _users[key];
        _users[key] = new UserEntry(entry.Name, newPassword, entry.Language);
        Save();
        message = "Senha alterada com sucesso.";
        return true;
    }

    public bool Delete(string username, out string message)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            message = "Nome de usuário obrigatório.";
            return false;
        }

        var key = username.Trim();
        if (!_users.ContainsKey(key))
        {
            message = "Usuário não encontrado.";
            return false;
        }

        if (_users.Count <= 1)
        {
            message = "Não é possível apagar o último usuário.";
            return false;
        }

        _users.Remove(key);
        Save();
        message = "Usuário removido com sucesso.";
        return true;
    }

    private static Dictionary<string, UserEntry> LoadUsers(string path)
    {
        var users = new Dictionary<string, UserEntry>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            return users;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var rawLine = line.Trim();
            if (string.IsNullOrEmpty(rawLine) || rawLine.StartsWith(";"))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex < 1)
            {
                continue;
            }

            var name = rawLine[..equalsIndex].Trim();
            var rest = rawLine[(equalsIndex + 1)..].Trim();
            var password = rest;
            var language = "EN-US";

            if (rest.Contains("|"))
            {
                var parts = rest.Split('|', 2);
                password = parts[0];
                language = parts[1];
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                continue;
            }

            users[name] = new UserEntry(name, password, language);
        }

        return users;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
        var lines = _users.Values.Select(u => $"{u.Name}={u.Password}|{u.Language}");
        File.WriteAllLines(_path, lines);
    }

    private sealed record UserEntry(string Name, string Password, string Language);
}
