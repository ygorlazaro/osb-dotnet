using System.Diagnostics;
using Osb.Shell.Apps;
using Osb.Shell.Games;

namespace Osb.Shell.Kernel;

/// <summary>
/// Porte da SUB Command(Comando$) do OSB.BAS original: o loop principal do
/// interpretador de comandos do OSB. Cada bloco IF do BASIC virou um "case"
/// aqui, mantendo a mesma ordem e o mesmo comportamento sempre que possível.
/// </summary>
public class OsbShell
{
    private readonly OsbEnvironment _env;
    private readonly List<string> _history = new();
    private int _historyIndex;
    private string _lastCommand = "";
    private string _lastRaw = "";
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

    private static void PrintStatusLine()
    {
        var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        var cwd = Directory.GetCurrentDirectory();
        var width = Math.Max(0, Console.WindowWidth - timestamp.Length);
        if (cwd.Length > width)
            cwd = "..." + cwd[(cwd.Length - width + 3)..];
        Console.WriteLine(cwd.PadRight(width) + timestamp);
    }

    // ---- Histórico de comandos (persistido em ~/.osb/HISTORY.TXT, estilo DOSKEY) ----

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var lines = File.ReadAllLines(HistoryFile).Where(l => l.Length > 0).ToList();
            if (lines.Count > MaxHistoryEntries)
                lines = lines.Skip(lines.Count - MaxHistoryEntries).ToList();
            _history.AddRange(lines);
        }
        catch
        {
            // histórico é conveniência, não motivo pra travar o boot do OSB.
        }
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(_env.HomeDir);
            File.WriteAllLines(HistoryFile, _history);
        }
        catch
        {
            // idem: se não der pra salvar (disco cheio, permissão etc.), segue o jogo.
        }
    }

    private void AddToHistory(string line)
    {
        if (_history.Count == 0 || _history[^1] != line)
            _history.Add(line);

        if (_history.Count > MaxHistoryEntries)
            _history.RemoveRange(0, _history.Count - MaxHistoryEntries);

        SaveHistory();
    }

    /// <summary>Comando HISTORY: lista o histórico numerado e pergunta se quer repetir algum.</summary>
    private void RunHistory(string args)
    {
        if (_history.Count == 0)
        {
            Console.WriteLine("Nenhum comando no histórico ainda.");
            return;
        }

        var count = 100;
        if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out var requestedCount) && requestedCount > 0)
            count = requestedCount;

        count = Math.Min(count, _history.Count);
        var startIndex = _history.Count - count;
        const int pageSize = 20;
        for (int i = 0; i < count; i++)
        {
            Console.WriteLine($"{i + 1,4}  {_history[startIndex + i]}");
            if ((i + 1) % pageSize == 0 && i + 1 < count)
            {
                Console.Write("-----Pressione ENTER para continuar----");
                Console.ReadLine();
            }
        }

        Console.Write("Repetir qual número (ENTER para nenhum)? ");
        var input = (Console.ReadLine() ?? "").Trim();
        if (input == "") return;

        if (int.TryParse(input, out var n) && n >= 1 && n <= count)
        {
            var cmd = _history[startIndex + n - 1];
            Console.WriteLine(cmd);
            Execute(cmd);
        }
        else
        {
            Console.WriteLine("Número inválido.");
        }
    }

    private void HandleHostname(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine(_env.MachineName);
            return;
        }

        if (!_isAuthenticated)
        {
            Console.WriteLine("Você deve estar autenticado para alterar o nome da máquina.");
            return;
        }

        _env.SaveMachineName(args.Trim());
        Console.WriteLine("Nome da máquina definido para: " + _env.MachineName);
    }

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
            default:
                PrintUserHelp();
                break;
        }
    }

    private void PromptLogin()
    {
        while (!_isAuthenticated)
        {
            var username = string.Empty;
            Console.Write("Usuário: ");
            username = (Console.ReadLine() ?? string.Empty).Trim();

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
                        Console.WriteLine("Senha incorreta. Tente novamente.");
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

    private void AddUser(string name, string password)
    {
        if (_env.Users.Add(name, password, out var message))
            Console.WriteLine(message);
        else
            Console.WriteLine(message);
    }

    private void ChangeUserPassword(string name, string password)
    {
        if (_env.Users.ChangePassword(name, password, out var message))
            Console.WriteLine(message);
        else
            Console.WriteLine(message);
    }

    private void DeleteUser(string name)
    {
        if (_env.Users.Delete(name, out var message))
            Console.WriteLine(message);
        else
            Console.WriteLine(message);
    }

    private void PrintUserHelp()
    {
        Console.WriteLine("Uso: USER [Enter]   → autentica");
        Console.WriteLine("     USER ADD <nome> <senha>   → adiciona usuário");
        Console.WriteLine("     USER CHANGE <nome> <senha>   → altera senha");
        Console.WriteLine("     USER DEL <nome>   → exclui usuário");
    }

    public void Execute(string rawInput)
    {
        // "raw" preserva o case original (importante para nomes de arquivo em sistemas
        // case-sensitive como Linux/macOS); "command" é a versão maiúscula usada apenas
        // para identificar a palavra-chave do comando, exatamente como no BASIC original.
        var raw = rawInput.Trim();
        var command = raw.ToUpperInvariant();
        if (command == "RPT") { raw = _lastRaw; command = _lastCommand; }
        if (command == "") return;
        _lastCommand = command;
        _lastRaw = raw;

        var spaceIndex = raw.IndexOf(' ');
        var verb = spaceIndex < 0 ? command : command[..spaceIndex];

        if (!_isAuthenticated && verb != "USER" && verb != "HOSTNAME")
        {
            Console.WriteLine("Você deve entrar com login. Use USER para autenticar.");
            return;
        }

        // Troca de "drive" (ex: C:) - conceito herdado do DOS, sem efeito real fora do Windows.
        if (command.Length == 2 && command[1] == ':')
        {
            Console.WriteLine("Conceito de drive não se aplica neste sistema operacional.");
            return;
        }

        if (command.EndsWith("/?"))
        {
            HelpTexts.Show(raw[..^2].TrimEnd());
            return;
        }

        var args = spaceIndex < 0 ? "" : raw[(spaceIndex + 1)..].Trim();

        switch (verb)
        {
            case "ABOUT": About.Show(); break;
            case "APLIC": RunAplic(args); break;
            case "CAL": Calendar.Show(args); break;
            case "CD": ChangeDirectory(args); break;
            case "CLS": case "CLEAR": Console.Clear(); break;
            case "COLOR": ColorPicker.Run(_env); break;
            case "CONFIG": ConfigUtility.Run(_env); break;
            case "COPY": CopyFile(); break;
            case "DATE":
                Console.WriteLine("Data atual: " + DateTime.Now.ToString("dd/MM/yyyy"));
                Console.WriteLine("(Alterar a data do sistema não é suportado nesta versão portada.)");
                break;
            case "DIR": ListDirectory(args); break;
            case "PWD": Console.WriteLine(Directory.GetCurrentDirectory()); break;
            case "ERASE": EraseFiles(args); break;
            case "EXIT": DoExit(); break;
            case "GAMES": RunGames(args); break;
            case "HELP": HelpTexts.Show(args); break;
            case "HISTORY": RunHistory(args); break;
            case "HOSTNAME": HandleHostname(args); break;
            case "KISS": TextEditor.Run(args, _env); break;
            case "MD": MakeDirectory(args); break;
            case "PRINT": PrintFile(args); break;
            case "RD": RemoveDirectory(args); break;
            case "REN": RenameFile(); break;
            case "SIZE": ShowSize(args); break;
            case "USER": HandleUser(args); break;
            case "TIME":
                Console.WriteLine("Hora atual: " + DateTime.Now.ToString("HH:mm:ss"));
                Console.WriteLine("(Alterar a hora do sistema não é suportado nesta versão portada.)");
                break;
            case "TREE": ShowTree(Directory.GetCurrentDirectory(), ""); break;
            case "TYPE": TypeFile(args); break;
            case "VER":
                Console.WriteLine("OSB Versão 0.2 (porte para .NET 10)");
                Console.WriteLine("Original: http://www.osb.rg3.net");
                Console.WriteLine();
                Console.WriteLine("Digite ABOUT para mais informações");
                break;
            case "X":
                XwinLauncher.Launch();
                // O XWIN mexe nas cores do terminal (e reseta pro padrão ao sair) - sem
                // isso, o OSB voltava cinza no branco em vez do esquema configurado.
                _env.ApplyColors();
                break;
            default:
                RunExternal(raw);
                break;
        }
    }

    // ---- Implementações auxiliares ----

    private void RunExternal(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd == "")
            return;

        try
        {
            Console.WriteLine("Executando um programa externo (fora do kernel).\n");
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows() ? $"/c {cmd}" : $"-c \"{cmd}\"",
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            Console.WriteLine("Final da execução");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Não foi possível executar: " + ex.Message);
        }
        finally
        {
            _env.ApplyColors();
        }
    }

    private void RunAplic(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "APLIC.CFG");
        var apps = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine("*** Aplicativos instalados no OSB ***");
            foreach (var app in apps)
                Console.WriteLine($"{app.Name} - {app.Description}");
            Console.WriteLine();
            Console.WriteLine("Use: APLIC <nome>  (ex: APLIC CAL)");
            return;
        }

        var entry = apps.FirstOrDefault(a => a.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine("Aplicativo não encontrado: " + arg);
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "CAL": Calendar.Show(""); break;
            case "KISS": TextEditor.Run("", _env); break;
            case "PROG": Console.WriteLine("PROG (teste de digitação) ainda não foi portado para .NET."); break;
            default: Console.WriteLine("Aplicativo não portado para .NET: " + entry.Name); break;
        }
    }

    private void RunGames(string arg)
    {
        var cfgPath = Path.Combine(_env.ConfDir, "GAMES.CFG");
        var games = ConfigFileParser.LoadEntries(cfgPath);
        arg = arg.Trim().ToUpperInvariant();

        if (arg == "")
        {
            Console.WriteLine("*** Games instalados no OSB ***");
            foreach (var game in games)
                Console.WriteLine($"{game.Name} - {game.Description}");
            Console.WriteLine();
            Console.Write("Entre com sua escolha (<ENTER> para sair): ");
            arg = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (arg == "") return;
        }

        var entry = games.FirstOrDefault(g => g.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            Console.WriteLine("Jogo não encontrado: " + arg);
            return;
        }

        switch (entry.Name.ToUpperInvariant())
        {
            case "HANGMAN": Hangman.Play(); break;
            default: Console.WriteLine("Jogo não portado para .NET: " + entry.Name); break;
        }
    }

    private static void ChangeDirectory(string target)
    {
        if (target == "") { HelpTexts.Show("CD"); return; }
        try
        {
            if (target == "..") Directory.SetCurrentDirectory("..");
            else if (target is "\\" or "/") Directory.SetCurrentDirectory(Path.GetPathRoot(Directory.GetCurrentDirectory())!);
            else Directory.SetCurrentDirectory(PathResolver.Resolve(target));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao mudar de diretório: " + ex.Message);
        }
    }

    private static void ListDirectory(string target)
    {
        var tokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pathTokens = new List<string>();
        var wide = false;
        foreach (var token in tokens)
        {
            if (token.Equals("/W", StringComparison.OrdinalIgnoreCase) || token.Equals("-W", StringComparison.OrdinalIgnoreCase))
                wide = true;
            else
                pathTokens.Add(token);
        }

        var dir = pathTokens.Count == 0 ? Directory.GetCurrentDirectory() : PathResolver.Resolve(string.Join(' ', pathTokens));
        Console.WriteLine("Exibindo o conteúdo do diretório:");
        try
        {
            var directories = Directory.GetDirectories(dir).OrderBy(x => x).Select(Path.GetFileName).ToArray();
            var files = Directory.GetFiles(dir).OrderBy(x => x).Select(Path.GetFileName).ToArray();
            if (wide)
            {
                var entries = directories.Select(d => $"<{d}>").Concat(files).ToArray();
                var columnWidth = Math.Max(10, Math.Min(25, Console.WindowWidth / 4));
                var columns = Math.Max(1, Console.WindowWidth / columnWidth);
                for (int i = 0; i < entries.Length; i += columns)
                {
                    var row = entries.Skip(i).Take(columns).Select(e => e.PadRight(columnWidth));
                    Console.WriteLine(string.Concat(row));
                }
                return;
            }

            foreach (var d in directories)
                Console.WriteLine("  <DIR>  " + d);
            foreach (var f in files)
            {
                var info = new FileInfo(Path.Combine(dir, f));
                Console.WriteLine($"  {info.Length,10}  {f}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao listar diretório: " + ex.Message);
        }
    }

    private static void MakeDirectory(string name)
    {
        if (name == "") { HelpTexts.Show("MD"); return; }
        try { Directory.CreateDirectory(name); }
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
    }

    private static void RemoveDirectory(string name)
    {
        if (name == "") { HelpTexts.Show("RD"); return; }
        try { Directory.Delete(PathResolver.Resolve(name)); }
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
    }

    private static void EraseFiles(string pattern)
    {
        if (pattern == "") { HelpTexts.Show("ERASE"); return; }
        Console.Write("Você tem certeza que deseja apagar o(s) arquivo(s)? (S/N) ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (answer != "S") return;

        if (pattern == ".") pattern = "*.*";
        try
        {
            var dirPart = Path.GetDirectoryName(pattern);
            var mask = Path.GetFileName(pattern);
            var dir = string.IsNullOrEmpty(dirPart) ? "." : PathResolver.Resolve(dirPart);
            Console.WriteLine("Excluindo...");
            foreach (var f in Directory.GetFiles(dir, mask))
                File.Delete(f);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void RenameFile()
    {
        Console.Write("Entre com o nome antigo: ");
        var oldName = Console.ReadLine() ?? "";
        Console.Write("Entre com o nome novo: ");
        var newName = Console.ReadLine() ?? "";
        if (oldName == "" || newName == "") return;
        try { File.Move(PathResolver.Resolve(oldName), newName); }
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
    }

    private static void CopyFile()
    {
        Console.Write("Entre com o arquivo de origem: ");
        var source = Console.ReadLine() ?? "";
        Console.Write("Entre com o arquivo de destino: ");
        var dest = Console.ReadLine() ?? "";
        try
        {
            var lines = File.ReadAllLines(PathResolver.Resolve(source));
            File.WriteAllLines(dest, lines);
            Console.WriteLine($"{lines.Length} linhas copiadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void ShowSize(string file)
    {
        if (file == "") { HelpTexts.Show("SIZE"); return; }
        try
        {
            var kb = new FileInfo(PathResolver.Resolve(file)).Length / 1024.0;
            Console.WriteLine($"{kb:0.##} KiloBytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void TypeFile(string file)
    {
        if (file == "") { HelpTexts.Show("TYPE"); return; }
        try
        {
            var lines = File.ReadAllLines(PathResolver.Resolve(file));
            int count = 0;
            foreach (var line in lines)
            {
                Console.WriteLine(line);
                count++;
                if (count % 20 == 0)
                {
                    Console.Write("-----Pressione ENTER para continuar----");
                    Console.ReadLine();
                }
            }
            Console.WriteLine($"{lines.Length} linhas");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void PrintFile(string file)
    {
        if (file == "") { HelpTexts.Show("PRINT"); return; }
        try
        {
            Console.WriteLine("Imprimindo " + file);
            Console.WriteLine("(Nenhuma impressora configurada - exibindo o conteúdo)");
            foreach (var line in File.ReadAllLines(PathResolver.Resolve(file))) Console.WriteLine(line);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void ShowTree(string dir, string indent)
    {
        try
        {
            Console.WriteLine(indent + Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)));
            foreach (var sub in Directory.GetDirectories(dir).OrderBy(x => x))
                ShowTree(sub, indent + "   ");
        }
        catch (Exception ex)
        {
            Console.WriteLine(indent + "Erro: " + ex.Message);
        }
    }

    private void DoExit()
    {
        Console.Write("Você tem certeza que deseja sair (S/N)? ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (answer != "S") return;

        Console.WriteLine("Finalizando os arquivos.");
        Console.WriteLine("Finalizando o kernel.");
        Console.WriteLine("OSB 0.2 encerrado.");
        _running = false;
    }

    private string ReadCommandLine()
    {
        var promptUser = _isAuthenticated && !string.IsNullOrWhiteSpace(_currentUsername)
            ? _currentUsername
            : "guest";
        Console.WriteLine(Directory.GetCurrentDirectory());
        Console.Write($"{promptUser}@{_env.MachineName} [@] ");

        var buffer = new List<char>();
        var cursor = 0;
        var editedLine = string.Empty;
        _historyIndex = _history.Count;

        // Movimento e reescrita usam só deslocamento RELATIVO de cursor (\e[nD / \e[nC),
        // nunca uma leitura da posição atual (Console.CursorTop/CursorLeft) - ler a posição
        // dispara uma consulta ao terminal (\e[6n, "onde você está?") que pode travar em
        // terminais que não respondem a isso rápido o bastante.
        void MoveLeft(int n) { if (n > 0) Console.Write($"\u001b[{n}D"); }
        void MoveRight(int n) { if (n > 0) Console.Write($"\u001b[{n}C"); }

        void RewriteTail(int extraErase)
        {
            var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
            Console.Write(tail + new string(' ', extraErase));
            MoveLeft(tail.Length + extraErase);
        }

        void ReplaceBuffer(string newValue)
        {
            MoveLeft(cursor);
            Console.Write(new string(' ', buffer.Count));
            MoveLeft(buffer.Count);
            buffer.Clear();
            buffer.AddRange(newValue);
            cursor = buffer.Count;
            Console.Write(new string(buffer.ToArray()));
        }

        while (true)
        {
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var line = new string(buffer.ToArray());
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        AddToHistory(line);
                    }
                    _historyIndex = _history.Count;
                    return line;
                case ConsoleKey.LeftArrow:
                    if (cursor > 0) { cursor--; MoveLeft(1); }
                    break;
                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Count) { cursor++; MoveRight(1); }
                    break;
                case ConsoleKey.Home:
                    MoveLeft(cursor);
                    cursor = 0;
                    break;
                case ConsoleKey.End:
                    MoveRight(buffer.Count - cursor);
                    cursor = buffer.Count;
                    break;
                case ConsoleKey.UpArrow:
                    if (_history.Count == 0) break;
                    if (_historyIndex == _history.Count)
                        editedLine = new string(buffer.ToArray());
                    _historyIndex = Math.Max(0, _historyIndex - 1);
                    ReplaceBuffer(_history[_historyIndex]);
                    break;
                case ConsoleKey.DownArrow:
                    if (_history.Count == 0) break;
                    _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                    ReplaceBuffer(_historyIndex < _history.Count ? _history[_historyIndex] : editedLine);
                    break;
                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                        MoveLeft(1);
                        RewriteTail(extraErase: 1);
                    }
                    break;
                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                    {
                        buffer.RemoveAt(cursor);
                        RewriteTail(extraErase: 1);
                    }
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        var tail = new string(buffer.ToArray(), cursor, buffer.Count - cursor);
                        Console.Write(key.KeyChar + tail);
                        MoveLeft(tail.Length);
                    }
                    break;
            }
        }
    }
}
