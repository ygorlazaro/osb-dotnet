using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private static void PrintStatusLine()
    {
        var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        var cwd = Directory.GetCurrentDirectory();
        var width = Math.Max(0, Console.WindowWidth - timestamp.Length);
        if (cwd.Length > width)
        {
            cwd = "..." + cwd[(cwd.Length - width + 3)..];
        }

        Console.WriteLine(cwd.PadRight(width) + timestamp);
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFile))
            {
                return;
            }

            var lines = File.ReadAllLines(HistoryFile).Where(l => l.Length > 0).ToList();
            if (lines.Count > MaxHistoryEntries)
            {
                lines = lines.Skip(lines.Count - MaxHistoryEntries).ToList();
            }

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
        {
            _history.Add(line);
        }

        if (_history.Count > MaxHistoryEntries)
        {
            _history.RemoveRange(0, _history.Count - MaxHistoryEntries);
        }

        SaveHistory();
    }

    private void RunHistory(string args)
    {
        if (_history.Count == 0)
        {
            Console.WriteLine("Nenhum comando no histórico ainda.");
            return;
        }

        var count = 100;
        if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out var requestedCount) && requestedCount > 0)
        {
            count = requestedCount;
        }

        count = Math.Min(count, _history.Count);
        var startIndex = _history.Count - count;
        const int pageSize = 20;
        for (var i = 0; i < count; i++)
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
        if (input == "")
        {
            return;
        }

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

    private void DoExit()
    {
        Console.Write("Você tem certeza que deseja sair (S/N)? ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (answer != "S")
        {
            return;
        }

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
        Console.WriteLine();
        Console.WriteLine(Directory.GetCurrentDirectory());
        Console.Write($"{promptUser}@{_env.MachineName} [@] ");

        var buffer = new List<char>();
        var cursor = 0;
        var editedLine = string.Empty;
        _historyIndex = _history.Count;
        // Ensure we receive Ctrl+C as input while editing so we can treat it
        // as "cancel line". We will restore previous value when done.
        var prevTreatControl = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;
        try
        {
            void MoveLeft(int n)
        {
            if (n > 0)
            {
                Console.Write($"\u001b[{n}D");
            }
        }

        void MoveRight(int n)
        {
            if (n > 0)
            {
                Console.Write($"\u001b[{n}C");
            }
        }

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
                // If user pressed Ctrl+C we cancel the current input line.
                if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    Console.WriteLine("^C");
                    return string.Empty;
                }
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
                        Console.TreatControlCAsInput = prevTreatControl;
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
                    if (_history.Count == 0)
                    {
                        break;
                    }

                    if (_historyIndex == _history.Count)
                    {
                        editedLine = new string(buffer.ToArray());
                    }

                    _historyIndex = Math.Max(0, _historyIndex - 1);
                    ReplaceBuffer(_history[_historyIndex]);
                    break;
                case ConsoleKey.DownArrow:
                    if (_history.Count == 0)
                    {
                        break;
                    }

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
        finally
        {
            // restore TreatControlCAsInput even if we return early (Ctrl+C handled above)
            Console.TreatControlCAsInput = prevTreatControl;
        }
    }
}
