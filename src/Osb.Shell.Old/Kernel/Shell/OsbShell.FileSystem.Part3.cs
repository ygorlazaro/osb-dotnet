using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private static void TypeFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("TYPE"); return; }

        var upperArgs = args.ToUpperInvariant();
        var pause = false;
        var pauseInterval = 20;
        var showLineNumbers = false;
        var file = args;

        var pIndex = upperArgs.IndexOf("/P");
        if (pIndex >= 0)
        {
            pause = true;
            var afterP = upperArgs[(pIndex + 2)..];
            var charsToRemove = 2;

            if (afterP.Length > 0 && afterP[0] == ':')
            {
                var numStr = afterP[1..].TakeWhile(char.IsDigit).ToArray();
                if (numStr.Length > 0 && int.TryParse(new string(numStr), out var num) && num > 0)
                {
                    pauseInterval = num;
                }
                charsToRemove = 2 + 1 + numStr.Length;
            }

            file = args.Remove(pIndex, charsToRemove);
        }

        var nIndex = upperArgs.IndexOf("/N");
        if (nIndex >= 0)
        {
            showLineNumbers = true;
            var afterN = upperArgs[(nIndex + 2)..];
            var charsToRemove = 2;

            if (afterN.Length > 0 && afterN[0] == ':')
            {
                var numStr = afterN[1..].TakeWhile(char.IsDigit).ToArray();
                if (numStr.Length > 0)
                {
                    charsToRemove = 2 + 1 + numStr.Length;
                }
            }

            file = args.Remove(nIndex, charsToRemove);
        }

        file = file.Trim();

        if (file == "") { HelpTexts.Show("TYPE"); return; }
        try
        {
            var lines = File.ReadAllLines(PathResolver.Resolve(file));
            var width = Math.Max(20, Console.WindowWidth);
            var numberWidth = showLineNumbers ? lines.Length.ToString().Length : 0;
            var count = 0;
            foreach (var line in lines)
            {
                string text;
                var fileType = OslangHighlighter.GetFileType(file);
                if (fileType == "OSLANG" || fileType == "CONFIG" || fileType == "HELP" || fileType == "WORDS")
                {
                    text = OslangHighlighter.Highlight(line, width);
                }
                else
                {
                    text = line;
                }

                if (showLineNumbers)
                {
                    var lineNumber = (count + 1).ToString().PadLeft(numberWidth);
                    Console.WriteLine($"{lineNumber}: {text}");
                }
                else
                {
                    Console.WriteLine(text);
                }

                count++;
                if (pause && count % pauseInterval == 0)
                {
                    Console.Write(I18nService.Get("fs.press_enter"));
                    Console.ReadLine();
                }
            }
            Console.WriteLine($"{lines.Length} " + I18nService.Get("fs.lines", lines.Length));
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
    private static void PrintFile(string file)
    {
        if (file == "") { HelpTexts.Show("PRINT"); return; }
        
        try
        {
            var resolved = PathResolver.Resolve(file);
            if (File.Exists(resolved))
            {
                Console.WriteLine(I18nService.Get("fs.printing", file));
                Console.WriteLine(I18nService.Get("fs.no_printer"));
                foreach (var line in File.ReadAllLines(resolved)) Console.WriteLine(line);
                return;
            }
        }
        catch { }
        
        if (TryEvaluateMath(file, out var result))
        {
            Console.WriteLine(result);
        }
        else
        {
            Console.WriteLine(file);
        }
    }
    private void SetPromptLayout(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine(I18nService.Get("fs.usage_prompt"));
            Console.WriteLine(I18nService.Get("fs.markers"));
            Console.WriteLine(I18nService.Get("fs.current_layout", _env.Prompt.Layout));
            return;
        }
        
        _env.Prompt.Layout = args.Trim();
        _env.Prompt.Save(_env.HomeDir);
        Console.WriteLine(I18nService.Get("fs.layout_updated"));
    }
    private static void MoveFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("MOVE"); return; }
        
        var trimmed = args.Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            Console.WriteLine(I18nService.Get("fs.usage_move"));
            return;
        }
        
        var source = trimmed[..lastSpace].Trim();
        var dest = trimmed[(lastSpace + 1)..].Trim();
        
        try
        {
            var sourceDir = Path.GetDirectoryName(source) ?? ".";
            var sourcePattern = Path.GetFileName(source);
            var resolvedDest = PathResolver.Resolve(dest);

            if (sourcePattern.Contains("*") || sourcePattern.Contains("?"))
            {
                var files = Directory.GetFiles(sourceDir, sourcePattern);
                if (files.Length == 0)
                {
                    Console.WriteLine(I18nService.Get("fs.no_files_found"));
                    return;
                }

                var targetIsDir = Directory.Exists(resolvedDest);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var targetPath = targetIsDir ? Path.Combine(resolvedDest, fileName) : resolvedDest;
                    File.Move(file, targetPath);
                    Console.WriteLine($"{I18nService.Get("fs.moved", fileName, targetPath)}");
                }
                Console.WriteLine($"{files.Length} " + I18nService.Get("fs.files_moved", files.Length));
            }
            else
            {
                var resolvedSource = PathResolver.Resolve(source);
                if (Directory.Exists(resolvedDest))
                {
                    var fileName = Path.GetFileName(resolvedSource);
                    resolvedDest = Path.Combine(resolvedDest, fileName);
                }
                File.Move(resolvedSource, resolvedDest);
                Console.WriteLine(I18nService.Get("fs.moved", source, dest));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
}
