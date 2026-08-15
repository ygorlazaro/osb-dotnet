namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    public void RequireLogin() => PromptLogin();

    private void PromptLogin()
    {
        while (!_isAuthenticated)
        {
            Console.Write(I18nService.Get("auth.username"));
            var username = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine(I18nService.Get("auth.username_required"));
                continue;
            }

            var attempt = 0;
            while (attempt < 3 && !_isAuthenticated)
            {
                var password = PromptForPassword(I18nService.Get("auth.password"));
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
                    attempt++;
                    if (attempt < 3)
                    {
                        Console.WriteLine(I18nService.Get("auth.incorrect_password"));
                    }
                }
            }

            if (!_isAuthenticated)
            {
                Console.WriteLine(I18nService.Get("auth.too_many_attempts"));
                Thread.Sleep(10_000);
            }
        }
    }

    private string PromptForPassword(string prompt)
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

            if (key.Key == ConsoleKey.Backspace && password.Count > 0)
            {
                password.RemoveAt(password.Count - 1);
                Console.Write("\b \b");
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Add(key.KeyChar);
            }
        }

        return new string(password.ToArray());
    }
}
