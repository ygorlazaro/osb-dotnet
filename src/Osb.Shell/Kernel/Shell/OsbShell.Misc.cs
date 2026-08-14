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

            foreach (var line in File.ReadAllLines(HistoryFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split('|', 3);
                if (parts.Length >= 3 && DateTime.TryParse(parts[0], out var timestamp))
                {
                    _history.Add(new HistoryEntry
                    {
                        Timestamp = timestamp,
                        Command = parts[2]
                    });
                }
                else if (parts.Length == 2)
                {
                    _history.Add(new HistoryEntry
                    {
                        Timestamp = DateTime.MinValue,
                        Command = parts[1]
                    });
                }
            }

            if (_history.Count > MaxHistoryEntries)
            {
                _history.RemoveRange(0, _history.Count - MaxHistoryEntries);
            }
        }
        catch
        {
        }
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
            var lines = _history.Select(h => $"{h.Timestamp:yyyy-MM-dd HH:mm:ss}|{h.Command}");
            File.WriteAllLines(HistoryFile, lines);
        }
        catch
        {
        }
    }

    private void AddToHistory(string line)
    {
        if (_history.Count == 0 || _history[^1].Command != line)
        {
            _history.Add(new HistoryEntry
            {
                Timestamp = DateTime.Now,
                Command = line
            });
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
        List<HistoryEntry> itemsToDisplay;

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
                .Where(h => h.Command.Trim().StartsWith(trimmedArgs, StringComparison.OrdinalIgnoreCase))
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
            var entry = itemsToDisplay[i];
            var timestamp = entry.Timestamp == DateTime.MinValue 
                ? "----/--/-- --:--:--" 
                : entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"{i + 1,4}  {timestamp}  {entry.Command}");
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
            var cmd = itemsToDisplay[n - 1].Command;
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

    private void ExecutePipeline(string rawInput)
    {
        var segments = rawInput.Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        if (segments.Length == 0) return;

        List<string>? inputLines = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;

            if (isLast)
            {
                if (inputLines != null)
                {
                    ExecuteWithPipedInput(segment, inputLines);
                }
                else
                {
                    Execute(segment);
                }
            }
            else
            {
                inputLines = CaptureCommandOutput(segment, inputLines);
            }
        }
    }

    private List<string> CaptureCommandOutput(string command, List<string>? inputLines)
    {
        var output = new List<string>();
        var sw = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(sw);

            if (inputLines != null)
            {
                ExecuteWithPipedInput(command, inputLines);
            }
            else
            {
                Execute(command);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var text = sw.ToString();
        if (!string.IsNullOrEmpty(text))
        {
            output.AddRange(text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
        }

        return output;
    }

    private void ExecuteWithPipedInput(string command, List<string> inputLines)
    {
        var trimmed = command.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        var verb = spaceIndex < 0 ? trimmed.ToUpperInvariant() : trimmed[..spaceIndex].ToUpperInvariant();
        var args = spaceIndex < 0 ? "" : trimmed[(spaceIndex + 1)..].Trim();

        switch (verb)
        {
            case "GREP":
                ExecuteGrep(args, inputLines);
                break;
            default:
                Execute(command);
                break;
        }
    }

    private void ExecuteGrep(string args, List<string>? inputLines = null)
    {
        if (inputLines == null || inputLines.Count == 0)
        {
            Console.WriteLine("Uso: GREP <padrao>");
            Console.WriteLine("Use em pipe: COMANDO ; GREP <padrao>");
            return;
        }

        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine("Uso: GREP <padrao>");
            return;
        }

        var pattern = args.Trim();
        foreach (var line in inputLines)
        {
            if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine(line);
            }
        }
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
                    ReplaceBuffer(_history[_historyIndex].Command);
                    break;
                case ConsoleKey.DownArrow:
                    if (_history.Count == 0)
                    {
                        break;
                    }

                    _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                    ReplaceBuffer(_historyIndex < _history.Count ? _history[_historyIndex].Command : editedLine);
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

    private string ExpandPrompt(string layout, string user, string hostname)
    {
        var result = layout;
        result = result.Replace("%user", user);
        result = result.Replace("%hostname", hostname);
        result = result.Replace("%pwd", Directory.GetCurrentDirectory());
        result = result.Replace("%d", DateTime.Now.ToString("dd/MM/yyyy"));
        result = result.Replace("%t", DateTime.Now.ToString("HH:mm:ss"));
        result = result.Replace("%br", Environment.NewLine);
        
        // Expand %VAR% from user variables
        var vars = _env.Variables.GetForUser(_env.CurrentUsername);
        foreach (var (name, value) in vars)
        {
            result = result.Replace($"%{name}%", value);
        }
        
        return result;
    }

    private string ExpandVariables(string input)
    {
        if (string.IsNullOrEmpty(_env.CurrentUsername)) return input;
        
        var vars = _env.Variables.GetForUser(_env.CurrentUsername);
        var result = input;
        foreach (var (name, value) in vars)
        {
            result = result.Replace($"%{name}%", value);
        }
        return result;
    }

    private static bool TryEvaluateMath(string expr, out double result)
    {
        result = 0;
        try
        {
            var s = expr.Replace(" ", "");
            if (s.Length == 0) return false;
            
            var pos = 0;
            
            double ParseFactor()
            {
                if (pos >= s.Length) return 0;
                
                if (s[pos] == '(')
                {
                    pos++;
                    var result = ParseExpression();
                    if (pos < s.Length && s[pos] == ')') pos++;
                    return result;
                }
                
                if (s[pos] == '-')
                {
                    pos++;
                    return -ParseFactor();
                }
                
                if (s[pos] == '+')
                {
                    pos++;
                    return ParseFactor();
                }
                
                var start = pos;
                while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
                    pos++;
                
                if (start == pos) return 0;
                
                return double.Parse(s[start..pos]);
            }
            
            double ParseTerm()
            {
                var result = ParseFactor();
                while (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
                {
                    var op = s[pos++];
                    var right = ParseFactor();
                    if (op == '*') result *= right;
                    else result /= right;
                }
                return result;
            }
            
            double ParseExpression()
            {
                var result = ParseTerm();
                while (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                {
                    var op = s[pos++];
                    var right = ParseTerm();
                    if (op == '+') result += right;
                    else result -= right;
                }
                return result;
            }
            
            result = ParseExpression();
            return pos >= s.Length;
        }
        catch
        {
            return false;
        }
    }
}
