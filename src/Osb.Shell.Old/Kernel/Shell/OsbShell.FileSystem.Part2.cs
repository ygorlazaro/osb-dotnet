using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private static void RecoverFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) { HelpTexts.Show("RECOVER"); return; }
        
        var trashDir = GetTrashPath();
        var sourcePath = Path.Combine(trashDir, fileName);
        
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            Console.WriteLine(I18nService.Get("fs.recover_not_found", fileName));
            return;
        }
        
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var destPath = Path.Combine(currentDir, fileName);
            
            if (File.Exists(destPath) || Directory.Exists(destPath))
            {
                Console.WriteLine(I18nService.Get("fs.recover_exists", fileName));
                return;
            }
            
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destPath);
            }
            
            Console.WriteLine(I18nService.Get("fs.recovered", fileName));
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
    private static void RenameFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) { HelpTexts.Show("REN"); return; }
        
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Console.WriteLine(I18nService.Get("fs.usage_ren"));
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
                Console.WriteLine(I18nService.Get("fs.dir_not_found"));
                return;
            }
            
            var hasWildcards = oldMask.Contains("*") || oldMask.Contains("?");
            
            if (hasWildcards)
            {
                var files = Directory.GetFiles(searchDir, oldMask);
                if (files.Length == 0)
                {
                    Console.WriteLine(I18nService.Get("fs.no_files_found"));
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
                        Console.WriteLine($"{I18nService.Get("fs.renamed", fileName, newFileName)}");
                    }
                }
                Console.WriteLine($"{files.Length} " + I18nService.Get("fs.files_renamed", files.Length));
            }
            else
            {
                var oldPath = PathResolver.Resolve(oldPattern);
                if (!File.Exists(oldPath))
                {
                    Console.WriteLine(I18nService.Get("fs.file_not_found_ren"));
                    return;
                }
                
                var newPath = PathResolver.Resolve(newPattern);
                File.Move(oldPath, newPath);
                Console.WriteLine($"{I18nService.Get("fs.renamed", oldPattern, newPattern)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
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
            Console.WriteLine(I18nService.Get("fs.usage_copy"));
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
                    File.Copy(file, targetPath, overwrite: true);
                    Console.WriteLine($"{I18nService.Get("fs.copied", fileName)}");
                }
                Console.WriteLine($"{files.Length} " + I18nService.Get("fs.files_copied", files.Length));
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
                Console.WriteLine($"{I18nService.Get("fs.copied", resolvedSource, resolvedDest)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
    private static void ShowSize(string file)
    {
        if (file == "") { HelpTexts.Show("SIZE"); return; }
        try
        {
            var kb = new FileInfo(PathResolver.Resolve(file)).Length / 1024.0;
            Console.WriteLine($"{kb:0.##} " + I18nService.Get("fs.kilobytes"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
}
