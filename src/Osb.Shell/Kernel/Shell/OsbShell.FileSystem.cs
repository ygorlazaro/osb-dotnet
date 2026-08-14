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
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
    }

    private static void RemoveDirectory(string name)
    {
        if (name == "") { HelpTexts.Show("RD"); return; }
        try { Directory.Delete(PathResolver.Resolve(name)); }
        catch (Exception ex) { Console.WriteLine("Erro: " + ex.Message); }
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
        
        Console.Write("Você tem certeza que deseja apagar o(s) arquivo(s)? (S/N) ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (answer != "S")
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
            Console.WriteLine("Excluindo...");
            
            if (recursive)
            {
                var files = Directory.GetFiles(dir, mask, SearchOption.AllDirectories);
                foreach (var f in files)
                    File.Delete(f);
                Console.WriteLine($"{files.Length} arquivo(s) excluído(s).");
            }
            else
            {
                var files = Directory.GetFiles(dir, mask);
                foreach (var f in files)
                    File.Delete(f);
                Console.WriteLine($"{files.Length} arquivo(s) excluído(s).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void RenameFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("REN"); return; }
        
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Console.WriteLine("Uso: REN <nome antigo> <nome novo>");
            return;
        }
        
        var oldPattern = parts[0];
        var newPattern = parts[1];
        
        try
        {
            var oldDir = Path.GetDirectoryName(oldPattern);
            var oldMask = Path.GetFileName(oldPattern);
            var searchDir = string.IsNullOrEmpty(oldDir) ? "." : PathResolver.Resolve(oldDir);
            
            if (!Directory.Exists(searchDir))
            {
                Console.WriteLine("Erro: Diretório não encontrado.");
                return;
            }
            
            var hasWildcards = oldMask.Contains("*") || oldMask.Contains("?");
            
            if (hasWildcards)
            {
                var files = Directory.GetFiles(searchDir, oldMask);
                if (files.Length == 0)
                {
                    Console.WriteLine("Nenhum arquivo encontrado.");
                    return;
                }
                
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var newFileName = ApplyWildcardRename(fileName, oldMask, newPattern);
                    var newPath = Path.Combine(searchDir, newFileName);
                    if (newPath != file)
                    {
                        File.Move(file, newPath);
                        Console.WriteLine($"Renomeado: {fileName} -> {newFileName}");
                    }
                }
                Console.WriteLine($"{files.Length} arquivo(s) renomeado(s).");
            }
            else
            {
                var oldPath = PathResolver.Resolve(oldPattern);
                if (!File.Exists(oldPath))
                {
                    Console.WriteLine("Erro: Arquivo não encontrado.");
                    return;
                }
                
                var newPath = PathResolver.Resolve(newPattern);
                File.Move(oldPath, newPath);
                Console.WriteLine($"Renomeado: {oldPattern} -> {newPattern}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }
    
    private static string ApplyWildcardRename(string fileName, string oldPattern, string newPattern)
    {
        var oldStar = oldPattern.IndexOf('*');
        var newStar = newPattern.IndexOf('*');
        
        if (oldStar >= 0 && newStar >= 0)
        {
            var prefix = oldPattern[..oldStar];
            var suffix = oldPattern[(oldStar + 1)..];
            var suffixLength = suffix.Length;
            
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && 
                (suffixLength == 0 || fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                var middle = suffixLength > 0 
                    ? fileName[prefix.Length..^suffixLength] 
                    : fileName[prefix.Length..];
                return newPattern.Replace("*", middle);
            }
        }
        
        return newPattern;
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
            var width = Math.Max(20, Console.WindowWidth);
            var count = 0;
            foreach (var line in lines)
            {
                string text;
                if (file.EndsWith(".osl", StringComparison.OrdinalIgnoreCase))
                {
                    text = OslangHighlighter.Highlight(line, width);
                }
                else
                {
                    text = line;
                }
                Console.WriteLine(text);
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
            var resolved = PathResolver.Resolve(file);
            if (File.Exists(resolved))
            {
                Console.WriteLine("Imprimindo " + file);
                Console.WriteLine("(Nenhuma impressora configurada - exibindo o conteúdo)");
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
            Console.WriteLine("Uso: PROMPT <layout>");
            Console.WriteLine("Marcadores: %user %hostname %pwd %d %t %br");
            Console.WriteLine("Layout atual: " + _env.Prompt.Layout);
            return;
        }
        
        _env.Prompt.Layout = args.Trim();
        _env.Prompt.Save(_env.HomeDir);
        Console.WriteLine("Layout do prompt atualizado.");
    }

    private static void MoveFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("MOVE"); return; }
        
        var trimmed = args.Trim();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            Console.WriteLine("Uso: MOVE <origem> <destino>");
            return;
        }
        
        var source = trimmed[..lastSpace].Trim();
        var dest = trimmed[(lastSpace + 1)..].Trim();
        
        try
        {
            var resolvedSource = PathResolver.Resolve(source);
            var resolvedDest = PathResolver.Resolve(dest);
            
            if (Directory.Exists(resolvedDest))
            {
                var fileName = Path.GetFileName(resolvedSource);
                resolvedDest = Path.Combine(resolvedDest, fileName);
            }
            
            File.Move(resolvedSource, resolvedDest);
            Console.WriteLine($"Movido: {source} -> {dest}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private static void FindFiles(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("FIND"); return; }
        
        var upperArgs = args.ToUpperInvariant();
        string? searchText = null;
        string? filePattern = null;
        var byNameOnly = false;
        
        if (upperArgs.StartsWith("/NAME "))
        {
            byNameOnly = true;
            filePattern = args[6..].Trim();
        }
        else if (upperArgs.StartsWith("/F "))
        {
            byNameOnly = true;
            filePattern = args[3..].Trim();
        }
        else
        {
            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            searchText = parts[0].Trim('"');
            if (parts.Length > 1)
            {
                filePattern = parts[1].Trim();
            }
        }
        
        try
        {
            var searchDir = Directory.GetCurrentDirectory();
            var matches = new List<string>();
            
            if (byNameOnly || filePattern != null)
            {
                var pattern = filePattern ?? "*";
                matches.AddRange(Directory.GetFiles(searchDir, pattern, SearchOption.AllDirectories));
            }
            else if (searchText != null)
            {
                var allFiles = Directory.GetFiles(searchDir, "*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        if (content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(file);
                        }
                    }
                    catch { }
                }
            }
            
            if (matches.Count == 0)
            {
                Console.WriteLine("Nenhum resultado encontrado.");
                return;
            }
            
            Console.WriteLine($"{matches.Count} resultado(s) encontrado(s):");
            foreach (var match in matches)
            {
                if (byNameOnly || filePattern != null)
                {
                    Console.WriteLine("  " + match);
                }
                else if (searchText != null)
                {
                    Console.WriteLine("  " + match);
                    try
                    {
                        var lines = File.ReadAllLines(match);
                        for (var i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            {
                                var start = Math.Max(0, i - 3);
                                var end = Math.Min(lines.Length - 1, i + 3);
                                
                                for (var j = start; j <= end; j++)
                                {
                                    var marker = j == i ? ">>> " : "    ";
                                    Console.WriteLine($"{marker}{j + 1}: {lines[j]}");
                                }
                                Console.WriteLine();
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }

    private void SetVariable(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            var vars = _env.Variables.GetForUser(_env.CurrentUsername);
            if (vars.Count == 0)
            {
                Console.WriteLine("Nenhuma variável definida.");
                return;
            }

            foreach (var (name, value) in vars)
            {
                Console.WriteLine($"{name}={value}");
            }
            return;
        }
        
        var equalsIndex = args.IndexOf('=');
        if (equalsIndex < 0)
        {
            var name = args.Trim();
            if (_env.Variables.TryGetValue(_env.CurrentUsername, name, out var value))
            {
                Console.WriteLine($"{name}={value}");
            }
            else
            {
                Console.WriteLine($"Variável '{name}' não definida.");
            }
            return;
        }
        
        var varName = args[..equalsIndex].Trim();
        var varValue = args[(equalsIndex + 1)..].Trim();
        
        if (string.IsNullOrEmpty(varValue))
        {
            _env.Variables.Remove(_env.CurrentUsername, varName);
            Console.WriteLine($"Variável '{varName}' removida.");
        }
        else
        {
            _env.Variables.Set(_env.CurrentUsername, varName, varValue);
            Console.WriteLine($"Variável '{varName}' definida.");
        }
        
        _env.Variables.Save(_env.HomeDir, _env.CurrentUsername);
    }
}
