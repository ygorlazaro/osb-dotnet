using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
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
            Console.WriteLine("Erro ao mudar de diretório: " + ex.Message);
        }
    }

    private static void ListDirectory(string args)
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
            Console.WriteLine("Erro ao listar diretório: Diretório não encontrado.");
            return;
        }

        Console.WriteLine("Exibindo o conteúdo do diretório:");
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
                var columnWidth = Math.Max(10, Math.Min(25, Console.WindowWidth / 4));
                var columns = Math.Max(1, Console.WindowWidth / columnWidth);
                for (var i = 0; i < entries.Length; i += columns)
                {
                    var row = entries.Skip(i).Take(columns).Select(e => e.PadRight(columnWidth));
                    Console.WriteLine(string.Concat(row));
                }
                return;
            }

            Console.WriteLine("  Criado em          Modificado em       Tamanho       Nome");
            Console.WriteLine("  ----------------  ----------------  ----------  ----------------");

            foreach (var d in directories)
            {
                Console.WriteLine($"  {FormatDate(d.CreationTime),-16}  {FormatDate(d.LastWriteTime),-16}       <DIR>  {d.Name}");
            }

            foreach (var f in files)
            {
                Console.WriteLine($"  {FormatDate(f.CreationTime),-16}  {FormatDate(f.LastWriteTime),-16}  {f.Length,10}  {f.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao listar diretório: " + ex.Message);
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
        if (answer != "S")
        {
            return;
        }

        if (pattern == ".")
        {
            pattern = "*.*";
        }

        try
        {
            var dirPart = Path.GetDirectoryName(pattern);
            var mask = Path.GetFileName(pattern);
            var dir = string.IsNullOrEmpty(dirPart) ? "." : PathResolver.Resolve(dirPart ?? ".");
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
        if (oldName == "" || newName == "")
        {
            return;
        }

        try { File.Move(PathResolver.Resolve(oldName), newName); }
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
    }

    private static void CopyFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("COPY"); return; }

        var trimmed = args.Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            Console.WriteLine("Uso: COPY <origem> <destino>");
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
                    Console.WriteLine("Nenhum arquivo encontrado.");
                    return;
                }

                var targetIsDir = Directory.Exists(resolvedDest);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var targetPath = targetIsDir ? Path.Combine(resolvedDest, fileName) : resolvedDest;
                    File.Copy(file, targetPath, overwrite: true);
                    Console.WriteLine($"Copiado: {fileName}");
                }
                Console.WriteLine($"{files.Length} arquivo(s) copiado(s).");
            }
            else
            {
                var resolvedSource = PathResolver.Resolve(source);
                if (Directory.Exists(resolvedDest))
                {
                    var fileName = Path.GetFileName(resolvedSource);
                    resolvedDest = Path.Combine(resolvedDest, fileName);
                }
                File.Copy(resolvedSource, resolvedDest, overwrite: true);
                Console.WriteLine($"Copiado: {resolvedSource} -> {resolvedDest}");
            }
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

    private static void TypeFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("TYPE"); return; }

        var upperArgs = args.ToUpperInvariant();
        var pause = false;
        var pauseInterval = 20;
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
        file = file.Trim();

        if (file == "") { HelpTexts.Show("TYPE"); return; }
        try
        {
            var lines = File.ReadAllLines(PathResolver.Resolve(file));
            var count = 0;
            foreach (var line in lines)
            {
                Console.WriteLine(line);
                count++;
                if (pause && count % pauseInterval == 0)
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
}
