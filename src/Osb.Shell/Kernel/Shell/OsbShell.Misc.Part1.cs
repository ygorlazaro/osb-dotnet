using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private void DrawStatusBar()
    {
        var user = _currentUsername;
        var host = _env.MachineName;
        var now = DateTime.Now;
        var time = now.ToString("dd/MM/yyyy HH:mm:ss");
        var line = $"{user}@{host} | {time}";

        try
        {
            var row = Math.Max(0, Console.WindowHeight - 1);
            Console.SetCursorPosition(0, row);

            Console.Write("\x1b[44;37m");
            Console.Write(line);

            var remaining = Console.WindowWidth - line.Length;
            if (remaining > 0)
            {
                Console.Write(new string(' ', remaining));
            }

            Console.Write("\x1b[0m");
        }
        catch (IOException)
        {
        }
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
            Console.WriteLine(I18nService.Get("commands.history_empty"));
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
                Console.WriteLine(I18nService.Get("commands.history_no_match", trimmedArgs));
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
                Console.Write(I18nService.Get("commands.history_press_enter"));
                Console.ReadLine();
            }
        }

        Console.Write(I18nService.Get("commands.repeat_number"));
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
            Console.WriteLine(I18nService.Get("commands.invalid_number"));
        }
    }
    private void DoExit()
    {
        Console.Write(I18nService.Get("commands.exit_prompt"));
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (!I18nService.IsAffirmative(answer))
        {
            return;
        }

        Console.WriteLine(I18nService.Get("commands.shutting_files"));
        Console.WriteLine(I18nService.Get("commands.shutting_kernel"));
        Console.WriteLine(I18nService.Get("commands.shutdown"));
        _running = false;
    }
    private void ExecutePipeline(string rawInput, bool requireAuth = true)
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
                    ExecuteWithPipedInput(segment, inputLines, requireAuth);
                }
                else
                {
                    Execute(segment, requireAuth);
                }
            }
            else
            {
                inputLines = CaptureCommandOutput(segment, inputLines, requireAuth);
            }
        }
    }
    private List<string> CaptureCommandOutput(string command, List<string>? inputLines, bool requireAuth = true)
    {
        var output = new List<string>();
        var sw = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(sw);

            if (inputLines != null)
            {
                ExecuteWithPipedInput(command, inputLines, requireAuth);
            }
            else
            {
                Execute(command, requireAuth);
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
    private void ExecuteWithPipedInput(string command, List<string> inputLines, bool requireAuth = true)
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
                Execute(command, requireAuth);
                break;
        }
    }
}
