namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private void HandleUser(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            _isAuthenticated = false;
            _currentUsername = string.Empty;
            PromptLogin();
            return;
        }

        var parts = args.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length > 0 ? parts[0].ToUpperInvariant() : string.Empty;

        switch (action)
        {
            case "ADD":
                if (!_isAuthenticated)
                {
                    Console.WriteLine("Você deve estar autenticado para adicionar usuários.");
                    return;
                }
                if (parts.Length >= 3)
                {
                    AddUser(parts[1], parts[2]);
                }
                else if (parts.Length == 2)
                {
                    var password = PromptForPassword("Senha: ");
                    AddUser(parts[1], password);
                }
                else
                {
                    Console.Write("Nome do usuário: ");
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    var password = PromptForPassword("Senha: ");
                    AddUser(name, password);
                }
                break;
            case "CHANGE":
                if (!_isAuthenticated)
                {
                    Console.WriteLine("Você deve estar autenticado para alterar senhas.");
                    return;
                }
                if (parts.Length >= 3)
                {
                    ChangeUserPassword(parts[1], parts[2]);
                }
                else if (parts.Length == 2)
                {
                    var password = PromptForPassword("Nova senha: ");
                    ChangeUserPassword(parts[1], password);
                }
                else
                {
                    Console.Write("Nome do usuário: ");
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    var password = PromptForPassword("Nova senha: ");
                    ChangeUserPassword(name, password);
                }
                break;
            case "DEL":
                if (!_isAuthenticated)
                {
                    Console.WriteLine("Você deve estar autenticado para excluir usuários.");
                    return;
                }
                if (parts.Length >= 2)
                {
                    DeleteUser(parts[1]);
                }
                else
                {
                    Console.Write("Nome do usuário: ");
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    DeleteUser(name);
                }
                break;
            case "LIST":
                if (!_isAuthenticated)
                {
                    Console.WriteLine("Você deve estar autenticado para listar usuários.");
                    return;
                }
                ListUsers();
                break;
            default:
                PrintUserHelp();
                break;
        }
    }

    private void ListUsers()
    {
        Console.WriteLine("Usuários cadastrados:");
        foreach (var user in _env.Users.Usernames)
        {
            Console.WriteLine("  " + user);
        }
    }

    private void DeleteUser(string name)
    {
        if (!_env.Users.Exists(name))
        {
            Console.WriteLine("Usuário não encontrado.");
            return;
        }

        var password = PromptForPassword("Senha do usuário ou do administrador: ");

        if (_env.Users.Validate(name, password) || _env.Users.Validate(_currentUsername, password))
        {
            if (_env.Users.Delete(name, out var message))
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
        else
        {
            Console.WriteLine("Senha incorreta.");
        }
    }

    private void AddUser(string name, string password)
    {
        if (_env.Users.Add(name, password, out var message))
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private void ChangeUserPassword(string name, string password)
    {
        if (_env.Users.ChangePassword(name, password, out var message))
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private void PrintUserHelp()
    {
        Console.WriteLine("Uso: USER [Enter]   → autentica");
        Console.WriteLine("     USER ADD <nome> <senha>   → adiciona usuário");
        Console.WriteLine("     USER CHANGE <nome> <senha>   → altera senha");
        Console.WriteLine("     USER DEL <nome>   → exclui usuário");
        Console.WriteLine("     USER LIST   → lista usuários cadastrados");
    }
}
