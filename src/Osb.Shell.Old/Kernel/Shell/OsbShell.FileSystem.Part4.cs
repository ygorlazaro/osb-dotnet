using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
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
                Console.WriteLine(I18nService.Get("fs.no_results"));
                return;
            }
            
            Console.WriteLine(I18nService.Get("fs.matches_found", matches.Count));
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
            Console.WriteLine(I18nService.Get("fs.error", ex.Message));
        }
    }
    private void SetVariable(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            var vars = _env.Variables.GetForUser(_env.CurrentUsername);
            if (vars.Count == 0)
            {
                Console.WriteLine(I18nService.Get("fs.no_variables_defined"));
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
                Console.WriteLine(I18nService.Get("fs.variable_not_defined", name));
            }
            return;
        }
        
        var varName = args[..equalsIndex].Trim();
        var varValue = args[(equalsIndex + 1)..].Trim();
        
        if (string.IsNullOrEmpty(varValue))
        {
            _env.Variables.Remove(_env.CurrentUsername, varName);
            Console.WriteLine(I18nService.Get("fs.variable_removed", varName));
        }
        else
        {
            _env.Variables.Set(_env.CurrentUsername, varName, varValue);
            Console.WriteLine(I18nService.Get("fs.variable_set", varName));
        }
        
        _env.Variables.Save(_env.HomeDir, _env.CurrentUsername);
    }
}
