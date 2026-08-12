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

    private static void ListDirectory(string target)
    {
        var tokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pathTokens = new List<string>();
        var wide = false;
        foreach (var token in tokens)
        {
            if (token.Equals("/W", StringComparison.OrdinalIgnoreCase) || token.Equals("-W", StringComparison.OrdinalIgnoreCase))
            {
                wide = true;
            }
            else
            {
                pathTokens.Add(token);
            }
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
                for (var i = 0; i < entries.Length; i += columns)
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
            var count = 0;
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
}
