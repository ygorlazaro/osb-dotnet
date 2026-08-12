using System.Linq;

namespace Osb.Shell.Kernel;

public sealed class UserManager
{
    private readonly string _path;
    private readonly Dictionary<string, (string Name, string Password)> _users;

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
            return false;

        return _users.TryGetValue(username.Trim(), out var entry) && entry.Password == password;
    }

    public bool Exists(string username)
    {
        return !string.IsNullOrWhiteSpace(username) && _users.ContainsKey(username.Trim());
    }

    public string GetDisplayName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return string.Empty;

        return _users.TryGetValue(username.Trim(), out var entry)
            ? entry.Name
            : username.Trim();
    }

    public bool Add(string username, string password, out string message)
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

        _users[username] = (username, password);
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
        _users[key] = (_users[key].Name, newPassword);
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

    private static Dictionary<string, (string Name, string Password)> LoadUsers(string path)
    {
        var users = new Dictionary<string, (string Name, string Password)>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
            return users;

        foreach (var line in File.ReadAllLines(path))
        {
            var rawLine = line.Trim();
            if (string.IsNullOrEmpty(rawLine) || rawLine.StartsWith(";"))
                continue;

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex < 1)
                continue;

            var name = rawLine[..equalsIndex].Trim();
            var password = rawLine[(equalsIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
                continue;

            users[name] = (name, password);
        }

        return users;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
        var lines = _users.Values.Select(u => $"{u.Name}={u.Password}");
        File.WriteAllLines(_path, lines);
    }
}
