namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private void PromptLogin()
    {
        while (!_isAuthenticated)
        {
            Console.Write("Usuário: ");
            var username = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Nome de usuário obrigatório.");
                continue;
            }

            var attempt = 0;
            while (attempt < 3 && !_isAuthenticated)
            {
                var password = PromptForPassword("Senha: ");
                if (_env.Users.Validate(username, password))
                {
                    _isAuthenticated = true;
                    _currentUsername = username;
                    Console.WriteLine("Autenticado como " + username + ".");
                }
                else
                {
                    attempt++;
                    if (attempt < 3)
                    {
                        Console.WriteLine("Senha incorreta. Tente novamente.");
                    }
                }
            }

            if (!_isAuthenticated)
            {
                Console.WriteLine("Muitas tentativas incorretas. Aguardando 10 segundos...");
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
