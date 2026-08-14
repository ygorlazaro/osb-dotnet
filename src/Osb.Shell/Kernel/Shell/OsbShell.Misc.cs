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

        var trimmedArgs = args.Trim();
        List<string> itemsToDisplay;

        if (string.IsNullOrWhiteSpace(trimmedArgs))
        {
            var count = Math.Min(100, _history.Count);
            var startIndex = _history.Count - count;
            itemsToDisplay = _history.GetRange(startIndex, count);
        }
        else if (int.TryParse(trimmedArgs, out var requestedCount) && requestedCount > 0)
        {
            var count = Math.Min(requestedCount, _history.Count);
            var startIndex = _history.Count - count;
            itemsToDisplay = _history.GetRange(startIndex, count);
        }
        else
        {
            itemsToDisplay = _history
                .Where(h => h.Trim().StartsWith(trimmedArgs, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (itemsToDisplay.Count == 0)
            {
                Console.WriteLine($"Nenhum comando no histórico começa com '{trimmedArgs}'.");
                return;
            }
        }

        const int pageSize = 20;
        for (var i = 0; i < itemsToDisplay.Count; i++)
        {
            Console.WriteLine($"{i + 1,4}  {itemsToDisplay[i]}");
            if ((i + 1) % pageSize == 0 && i + 1 < itemsToDisplay.Count)
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

        if (int.TryParse(input, out var n) && n >= 1 && n <= itemsToDisplay.Count)
        {
            var cmd = itemsToDisplay[n - 1];
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
        Console.WriteLine("OSB 3.0 Lince encerrado.");
        _running = false;
    }

    private string ReadCommandLine()
    {
        var promptUser = _isAuthenticated && !string.IsNullOrWhiteSpace(_currentUsername)
            ? _currentUsername
            : "guest";
        Console.WriteLine();
        Console.WriteLine(Directory.GetCurrentDirectory());
        Console.Write(ExpandPrompt(_env.Prompt.Layout, promptUser, _env.MachineName));

        var buffer = new List<char>();
        var cursor = 0;
        var editedLine = string.Empty;
        _historyIndex = _history.Count;

        var tabCompleter = new TabCompleter(_env);
        IReadOnlyList<string>? activeCandidates = null;
        var candidateIndex = -1;
        var completionTokenStart = -1;
        var isCompleting = false;

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

            void ReplaceToken(string newToken)
            {
                var oldTokenLength = cursor - completionTokenStart;
                if (oldTokenLength > 0)
                {
                    MoveLeft(oldTokenLength);
                }

                buffer.RemoveRange(completionTokenStart, oldTokenLength);
                buffer.InsertRange(completionTokenStart, newToken);

                var tail = new string(buffer.ToArray(), completionTokenStart, buffer.Count - completionTokenStart);
                Console.Write(tail);

                var extraErase = Math.Max(0, oldTokenLength - newToken.Length);
                if (extraErase > 0)
                {
                    Console.Write(new string(' ', extraErase));
                    MoveLeft(extraErase);
                }

                cursor = completionTokenStart + newToken.Length;
                MoveLeft(buffer.Count - cursor);
            }

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Tab)
                {
                    isCompleting = false;
                    activeCandidates = null;
                    candidateIndex = -1;
                }

                // If user pressed Ctrl+C we cancel the current input line.
                if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    Console.WriteLine("^C");
                    return string.Empty;
                }

                switch (key.Key)
                {
                    case ConsoleKey.Tab:
                        var currentText = new string(buffer.ToArray());
                        var isShift = (key.Modifiers & ConsoleModifiers.Shift) != 0;

                        if (!isCompleting || activeCandidates == null || activeCandidates.Count == 0)
                        {
                            var context = TabCompleter.ParseContext(currentText, cursor);
                            var candidates = tabCompleter.GetCandidates(currentText, cursor);

                            if (candidates.Count == 0)
                            {
                                break;
                            }

                            completionTokenStart = context.TokenStart;
                            var originalTokenLength = context.CurrentToken.Length;

                            if (candidates.Count == 1)
                            {
                                ReplaceToken(candidates[0]);
                                isCompleting = false;
                            }
                            else
                            {
                                var lcp = TabCompleter.GetLongestCommonPrefix(candidates);
                                var expandedLcp = false;

                                if (lcp.Length > originalTokenLength)
                                {
                                    ReplaceToken(lcp);
                                    expandedLcp = true;
                                }

                                activeCandidates = candidates;
                                isCompleting = true;
                                candidateIndex = isShift ? activeCandidates.Count - 1 : 0;

                                if (!expandedLcp)
                                {
                                    ReplaceToken(activeCandidates[candidateIndex]);
                                }
                            }
                        }
                        else
                        {
                            if (isShift)
                            {
                                candidateIndex = (candidateIndex - 1 + activeCandidates.Count) % activeCandidates.Count;
                            }
                            else
                            {
                                candidateIndex = (candidateIndex + 1) % activeCandidates.Count;
                            }

                            ReplaceToken(activeCandidates[candidateIndex]);
                        }
                        break;
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

    private static string ExpandPrompt(string layout, string user, string hostname)
    {
        var result = layout;
        result = result.Replace("%user", user);
        result = result.Replace("%hostname", hostname);
        result = result.Replace("%pwd", Directory.GetCurrentDirectory());
        result = result.Replace("%d", DateTime.Now.ToString("dd/MM/yyyy"));
        result = result.Replace("%t", DateTime.Now.ToString("HH:mm:ss"));
        result = result.Replace("%br", Environment.NewLine);
        return result;
    }
}
