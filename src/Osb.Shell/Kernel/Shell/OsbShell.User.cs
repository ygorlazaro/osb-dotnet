namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private void HandleUser(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            _isAuthenticated = false;
            _currentUsername = string.Empty;
            _env.SetCurrentUsername(string.Empty);
            PromptLogin();
            return;
        }

        var parts = args.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length > 0 ? parts[0].ToUpperInvariant() : string.Empty;

        if (action == "LOGIN" || (parts.Length >= 2 && !action.StartsWith("ADD") && !action.StartsWith("CHANGE") && !action.StartsWith("DEL") && !action.StartsWith("LIST")))
        {
            var username = parts[0];
            var password = parts.Length > 1 ? parts[1] : string.Empty;
            
            if (_env.Users.Validate(username, password))
            {
                _isAuthenticated = true;
                _currentUsername = username;
                _env.SetCurrentUsername(username);
                I18nService.SetLanguage(_env.CurrentLanguage);
                Console.WriteLine(I18nService.Get("auth.authenticated", username));
            }
            else
            {
                Console.WriteLine(I18nService.Get("auth.incorrect_password"));
            }
            return;
        }

        switch (action)
        {
            case "ADD":
                if (!_isAuthenticated)
                {
                    Console.WriteLine(I18nService.Get("user.must_be_authenticated_add"));
                    return;
                }
                if (parts.Length >= 3)
                {
                    AddUser(parts[1], parts[2]);
                }
                else if (parts.Length == 2)
                {
                    var password = PromptForPassword(I18nService.Get("user.enter_password"));
                    AddUser(parts[1], password);
                }
                else
                {
                    Console.Write(I18nService.Get("user.enter_username"));
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    var password = PromptForPassword(I18nService.Get("user.enter_password"));
                    AddUser(name, password);
                }
                break;
            case "CHANGE":
                if (!_isAuthenticated)
                {
                    Console.WriteLine(I18nService.Get("user.must_be_authenticated_change"));
                    return;
                }
                if (parts.Length >= 3)
                {
                    ChangeUserPassword(parts[1], parts[2]);
                }
                else if (parts.Length == 2)
                {
                    var password = PromptForPassword(I18nService.Get("user.new_password"));
                    ChangeUserPassword(parts[1], password);
                }
                else
                {
                    Console.Write(I18nService.Get("user.enter_username"));
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    var password = PromptForPassword(I18nService.Get("user.new_password"));
                    ChangeUserPassword(name, password);
                }
                break;
            case "DEL":
                if (!_isAuthenticated)
                {
                    Console.WriteLine(I18nService.Get("user.must_be_authenticated_delete"));
                    return;
                }
                if (parts.Length >= 2)
                {
                    DeleteUser(parts[1]);
                }
                else
                {
                    Console.Write(I18nService.Get("user.enter_username"));
                    var name = (Console.ReadLine() ?? string.Empty).Trim();
                    DeleteUser(name);
                }
                break;
            case "LIST":
                if (!_isAuthenticated)
                {
                    Console.WriteLine(I18nService.Get("user.must_be_authenticated_list"));
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
        Console.WriteLine(I18nService.Get("user.registered_users"));
        foreach (var user in _env.Users.Usernames)
        {
            Console.WriteLine("  " + user);
        }
    }

    private void DeleteUser(string name)
    {
        if (!_env.Users.Exists(name))
        {
            Console.WriteLine(I18nService.Get("user.not_found"));
            return;
        }

        var password = PromptForPassword(I18nService.Get("user.password_prompt"));

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
            Console.WriteLine(I18nService.Get("auth.incorrect_password"));
        }
    }

    private void AddUser(string name, string password)
    {
        var language = _env.CurrentLanguage;
        if (_env.Users.Add(name, password, language, out var message))
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
        Console.WriteLine(I18nService.Get("user.usage"));
        Console.WriteLine(I18nService.Get("user.login"));
        Console.WriteLine(I18nService.Get("user.add"));
        Console.WriteLine(I18nService.Get("user.change"));
        Console.WriteLine(I18nService.Get("user.delete"));
        Console.WriteLine(I18nService.Get("user.list"));
    }
}
