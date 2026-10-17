using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private static string GetTrashPath()
    {
        var trashDir = Path.Combine(Directory.GetCurrentDirectory(), ".trash");
        if (!Directory.Exists(trashDir))
        {
            Directory.CreateDirectory(trashDir);
        }
        return trashDir;
    }
    private static string GetUniqueTrashPath(string trashDir, string fileName)
    {
        var trashPath = Path.Combine(trashDir, fileName);
        if (!File.Exists(trashPath) && !Directory.Exists(trashPath))
        {
            return trashPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;
        while (true)
        {
            var newName = $"{nameWithoutExt} ({counter}){extension}";
            trashPath = Path.Combine(trashDir, newName);
            if (!File.Exists(trashPath) && !Directory.Exists(trashPath))
            {
                return trashPath;
            }
            counter++;
        }
    }
    private static void MoveToTrash(string fullPath)
    {
        var trashDir = GetTrashPath();
        var fileName = Path.GetFileName(fullPath);
        var trashPath = GetUniqueTrashPath(trashDir, fileName);
        
        if (File.Exists(fullPath))
        {
            File.Move(fullPath, trashPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Move(fullPath, trashPath);
        }
    }
    private static void ChangeDirectory(string target)
    {
        if (target == "") { HelpTexts.Show("CD"); return; }
        try
        {
            if (target == "..")
            {
                Directory.SetCurrentDirectory("..");
            }
            else if (target is "\\" or "/")
            {
                Directory.SetCurrentDirectory(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? ".");
            }
            else
            {
                Directory.SetCurrentDirectory(PathResolver.Resolve(target));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error_changing_dir", ex.Message));
        }
    }
    private void ListDirectory(string args)
    {
        var upperArgs = args.ToUpperInvariant();
        var wide = upperArgs.Contains("/W") || upperArgs.Contains("-W");

        var pathPart = args;
        foreach (var flag in new[] { "/W", "-W" })
        {
            var idx = upperArgs.IndexOf(flag, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                pathPart = pathPart.Remove(idx, flag.Length);
                upperArgs = upperArgs.Remove(idx, flag.Length);
            }
        }

        var dirPart = pathPart.Trim();
        var searchPattern = "*";
        var searchDir = string.IsNullOrEmpty(dirPart) ? Directory.GetCurrentDirectory() : PathResolver.Resolve(dirPart);

        if (!dirPart.Contains('\\') && !dirPart.Contains('/'))
        {
            var lastSep = searchDir.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSep >= 0)
            {
                var potentialPattern = searchDir[(lastSep + 1)..];
                var potentialDir = searchDir[..(lastSep + 1)];
                if (potentialPattern.Contains("*") || potentialPattern.Contains("?"))
                {
                    searchPattern = potentialPattern;
                    searchDir = potentialDir;
                }
            }
        }

        if (!Directory.Exists(searchDir))
        {
            Console.WriteLine(I18nService.Get("fs.directory_not_found_list"));
            return;
        }

        Console.WriteLine(I18nService.Get("fs.listing_directory"));
        try
        {
            var directories = Directory.GetDirectories(searchDir)
                .Where(d => MatchesPattern(Path.GetFileName(d), searchPattern))
                .OrderBy(x => x)
                .Select(x => new DirectoryInfo(x))
                .ToArray();

            var files = Directory.GetFiles(searchDir, searchPattern)
                .OrderBy(x => x)
                .Select(x => new FileInfo(x))
                .ToArray();

            if (wide)
            {
                var entries = directories.Select(d => $"<{d.Name}>").Concat(files.Select(f => f.Name)).ToArray();
                var isDir = directories.Select(d => true).Concat(files.Select(f => false)).ToArray();
                var columnWidth = Math.Max(10, Math.Min(25, Console.WindowWidth / 4));
                var columns = Math.Max(1, Console.WindowWidth / columnWidth);
                for (var i = 0; i < entries.Length; i += columns)
                {
                    for (var j = i; j < i + columns && j < entries.Length; j++)
                    {
                        if (isDir[j])
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                        }
                        Console.Write(entries[j].PadRight(columnWidth));
                        if (isDir[j])
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                    }
                    Console.WriteLine();
                }
                return;
            }

            Console.WriteLine(I18nService.Get("fs.header_created") + "          " + I18nService.Get("fs.header_modified") + "       " + I18nService.Get("fs.header_size") + "      " + I18nService.Get("fs.header_name"));
            Console.WriteLine(I18nService.Get("fs.header_separator"));

            foreach (var d in directories)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  {FormatDate(d.CreationTime),-16}  {FormatDate(d.LastWriteTime),-16}       <DIR>  {d.Name}");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            foreach (var f in files)
            {
                Console.WriteLine($"  {FormatDate(f.CreationTime),-16}  {FormatDate(f.LastWriteTime),-16}  {f.Length,10}  {f.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error_listing_dir", ex.Message));
        }
    }
    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern == "*") return true;
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    private static string FormatDate(DateTime date)
    {
        return date.ToString("dd/MM/yyyy HH:mm");
    }
    private static void MakeDirectory(string name)
    {
        if (name == "") { HelpTexts.Show("MD"); return; }
        
        var upperName = name.ToUpperInvariant();
        var createParents = upperName.Contains("/P");
        
        if (createParents)
        {
            name = name.Replace("/P", "", StringComparison.OrdinalIgnoreCase).Trim();
        }
        
        if (name == "") { HelpTexts.Show("MD"); return; }
        
        try
        {
            if (createParents)
            {
                Directory.CreateDirectory(PathResolver.Resolve(name));
            }
            else
            {
                Directory.CreateDirectory(PathResolver.Resolve(name));
            }
        }
        catch (Exception ex) { Console.WriteLine(I18nService.Get("fs.error", ex.Message)); }
    }
    private static void RemoveDirectory(string name)
    {
        if (name == "") { HelpTexts.Show("RD"); return; }
        try
        {
            var fullPath = PathResolver.Resolve(name);
            MoveToTrash(fullPath);
        }
        catch (Exception ex) { Console.WriteLine(I18nService.Get("fs.error", ex.Message)); }
    }
    private static void DeleteFiles(string pattern)
    {
        if (pattern == "") { HelpTexts.Show("DEL"); return; }
        
        var upperArgs = pattern.ToUpperInvariant();
        var recursive = upperArgs.Contains("/S");
        var actualPattern = pattern;
        
        if (recursive)
        {
            actualPattern = pattern.Replace("/S", "", StringComparison.OrdinalIgnoreCase).Trim();
        }
        
        Console.Write(I18nService.Get("fs.confirm_delete"));
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (!I18nService.IsAffirmative(answer))
        {
            return;
        }

        if (actualPattern == ".")
        {
            actualPattern = "*.*";
        }

        try
        {
            var dirPart = Path.GetDirectoryName(actualPattern);
            var mask = Path.GetFileName(actualPattern);
            var dir = string.IsNullOrEmpty(dirPart) ? "." : PathResolver.Resolve(dirPart ?? ".");
            Console.WriteLine(I18nService.Get("fs.deleting"));
            
            if (recursive)
            {
                var files = Directory.GetFiles(dir, mask, SearchOption.AllDirectories);
                foreach (var f in files)
                    MoveToTrash(f);
                Console.WriteLine($"{files.Length} " + I18nService.Get("fs.files_deleted", files.Length));
            }
            else
            {
                var files = Directory.GetFiles(dir, mask);
                foreach (var f in files)
                    MoveToTrash(f);
                Console.WriteLine($"{files.Length} " + I18nService.Get("fs.files_deleted", files.Length));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
}
