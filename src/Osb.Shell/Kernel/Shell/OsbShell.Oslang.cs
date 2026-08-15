using Osb.Lang;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;
using Osb.Shell.Apps;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    /// <summary>
    /// Comando OSL: executa um programa OSLANG (.osl).
    ///
    /// Uso: OSL arquivo.osl
    /// </summary>
    private void RunOslFile(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine("Uso: OSL <arquivo.osl>");
            return;
        }

        RunOslScript(PathResolver.Resolve(args.Trim()), []);
    }

    private void RunOslScript(string scriptPath, IReadOnlyList<string> commandArgs)
    {
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Arquivo não encontrado: {scriptPath}");
            return;
        }

        string source;
        try
        {
            source = File.ReadAllText(scriptPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao ler o arquivo: " + ex.Message);
            return;
        }

        var extensions = new ExtensionRegistry();
        RegisterOsbShellExtensions(extensions);
        var interpreter = new OslangInterpreter(extensions);

        try
        {
            var basePath = Path.GetDirectoryName(scriptPath) ?? string.Empty;
            var oslArgs = commandArgs.Select(a => new StringValue(a)).ToList();
            interpreter.Execute(source, Console.Out, Console.In, Console.Clear, basePath, oslArgs);
        }
        catch (OslangException ex)
        {
            Console.WriteLine(ex.ToDisplayString());
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro inesperado ao executar o script: " + ex.Message);
        }
    }

    private bool TryRunOslCommand(string commandName, string args)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "COMMANDS", commandName.ToUpperInvariant(), "main.osl");
        if (!File.Exists(scriptPath))
        {
            return false;
        }

        var parsedArgs = ParseArgList(args);
        RunOslScript(scriptPath, parsedArgs);
        return true;
    }

    /// <summary>
    /// Comando RUN: executa um script .osh (shell script simples).
    ///
    /// Uso: RUN arquivo.osh [arg1 arg2 ...]
    /// </summary>
    private void RunOshFile(string path, string[]? args = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Uso: RUN <arquivo.osh> [argumentos...]");
            return;
        }

        var resolvedPath = PathResolver.Resolve(path.Trim());

        if (!File.Exists(resolvedPath))
        {
            Console.WriteLine($"Arquivo não encontrado: {path}");
            return;
        }

        var runner = new OshScript(_env, this);
        runner.RunFile(resolvedPath, args);
    }

    private void RunOslHelp()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "src", "Osb.Lang", "OSLANG-0.2-SPEC.md"),
            Path.Combine(Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.FullName, "src", "Osb.Lang", "OSLANG-0.2-SPEC.md"),
            Path.Combine(Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName, "src", "Osb.Lang", "OSLANG-0.2-SPEC.md"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                TextEditor.Run(candidate, _env);
                return;
            }
        }

        Console.WriteLine("Arquivo de especificação OSLANG 0.2 não encontrado.");
        Console.WriteLine("Use HELP OSL para ver a referência rápida.");
    }

    /// <summary>
    /// Extension API de OSLANG (seção 45): expõe um subconjunto dos comandos do
    /// Shell como funções OSLANG, sem OSLANG nunca conhecer OsbShell diretamente
    /// - é este método (do lado do host) que faz a ponte.
    ///
    /// PWD() retorna o diretório atual como STRING. DIR/CD/MKDIR/DEL/TYPE
    /// reaproveitam exatamente os mesmos métodos privados usados pelos comandos
    /// de shell equivalentes (mesmo comportamento, mesmas mensagens de erro em
    /// Console.WriteLine) e retornam NULL - eles são efeitos colaterais, não
    /// expressões com valor útil, então NULL é a escolha mais simples (seção 55:
    /// "prefer simplicity").
    /// </summary>
    private static void RegisterOsbShellExtensions(ExtensionRegistry extensions)
    {
        extensions.Register("PWD", (args, location) =>
        {
            RequireArgCount(args, 0, "PWD", location);
            return new StringValue(Directory.GetCurrentDirectory());
        });

        extensions.Register("CD", (args, location) =>
        {
            var target = RequireStringArg(args, 0, "CD", location);
            ChangeDirectory(target);
            return OslangValue.Null;
        });

        extensions.Register("MKDIR", (args, location) =>
        {
            var name = RequireStringArg(args, 0, "MKDIR", location);
            MakeDirectory(name);
            return OslangValue.Null;
        });

        extensions.Register("DEL", (args, location) =>
        {
            var pattern = RequireStringArg(args, 0, "DEL", location);
            DeleteFiles(pattern);
            return OslangValue.Null;
        });

        extensions.Register("TYPE", (args, location) =>
        {
            var file = RequireStringArg(args, 0, "TYPE", location);
            TypeFile(file);
            return OslangValue.Null;
        });

        extensions.Register("RANDOM", (args, location) =>
        {
            RequireArgCount(args, 1, "RANDOM", location);
            var max = (int)RequireNumberArg(args, 0, "RANDOM", location);
            if (max <= 0)
            {
                throw new OslangRuntimeException(location, "RANDOM() max must be greater than 0.");
            }
            var rnd = new Random();
            return new NumberValue(rnd.Next(max));
        });

        extensions.Register("READLINES", (args, location) =>
        {
            RequireArgCount(args, 1, "READLINES", location);
            var path = RequireStringArg(args, 0, "READLINES", location);
            var resolvedPath = path;
            if (!Path.IsPathFullyQualified(path) && !string.IsNullOrEmpty(extensions.BasePath))
            {
                var candidate = Path.GetFullPath(Path.Combine(extensions.BasePath, path));
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                }
            }
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"READLINES() file not found: {path}");
            }
            var lines = File.ReadAllLines(resolvedPath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var items = lines.Select(l => (OslangValue)new StringValue(l)).ToList();
            return new ArrayValue(items, RuntimeType.String);
        });

        extensions.Register("CHARAT", (args, location) =>
        {
            RequireArgCount(args, 2, "CHARAT", location);
            var str = RequireStringArg(args, 0, "CHARAT", location);
            var index = (int)RequireNumberArg(args, 1, "CHARAT", location);
            if (index < 0 || index >= str.Length)
            {
                throw new OslangRuntimeException(location, "CHARAT() index out of range.");
            }
            return new StringValue(str[index].ToString());
        });

        extensions.Register("UCASE", (args, location) =>
        {
            RequireArgCount(args, 1, "UCASE", location);
            var str = RequireStringArg(args, 0, "UCASE", location);
            return new StringValue(str.ToUpperInvariant());
        });

        extensions.Register("NORMALIZE", (args, location) =>
        {
            RequireArgCount(args, 1, "NORMALIZE", location);
            var text = RequireStringArg(args, 0, "NORMALIZE", location);
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var builder = new System.Text.StringBuilder();
            foreach (var ch in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }
            var result = builder.ToString().Normalize(System.Text.NormalizationForm.FormC)
                .Replace('Ç', 'C')
                .Replace('ç', 'C');
            return new StringValue(result.ToUpperInvariant());
        });

        extensions.Register("FILE.EXISTS", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.EXISTS", location);
            var path = RequireStringArg(args, 0, "FILE.EXISTS", location);
            return BooleanValue.Of(File.Exists(ResolvePath(path, extensions)));
        });

        extensions.Register("FILE.READ", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.READ", location);
            var path = RequireStringArg(args, 0, "FILE.READ", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.READ() file not found: {path}");
            }
            var lines = File.ReadAllLines(resolvedPath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var items = lines.Select(l => (OslangValue)new StringValue(l)).ToList();
            return new ArrayValue(items, RuntimeType.String);
        });

        extensions.Register("FILE.READTEXT", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.READTEXT", location);
            var path = RequireStringArg(args, 0, "FILE.READTEXT", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.READTEXT() file not found: {path}");
            }
            return new StringValue(File.ReadAllText(resolvedPath));
        });

        extensions.Register("FILE.WRITE", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.WRITE", location);
            var path = RequireStringArg(args, 0, "FILE.WRITE", location);
            var text = RequireStringArg(args, 1, "FILE.WRITE", location);
            var resolvedPath = ResolvePath(path, extensions);
            File.WriteAllText(resolvedPath, text);
            return OslangValue.Null;
        });

        extensions.Register("FILE.APPEND", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.APPEND", location);
            var path = RequireStringArg(args, 0, "FILE.APPEND", location);
            var text = RequireStringArg(args, 1, "FILE.APPEND", location);
            var resolvedPath = ResolvePath(path, extensions);
            File.AppendAllText(resolvedPath, text);
            return OslangValue.Null;
        });

        extensions.Register("FILE.CREATE", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.CREATE", location);
            var path = RequireStringArg(args, 0, "FILE.CREATE", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.CREATE() file already exists: {path}");
            }
            File.WriteAllText(resolvedPath, string.Empty);
            return OslangValue.Null;
        });

        extensions.Register("FILE.DELETE", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.DELETE", location);
            var path = RequireStringArg(args, 0, "FILE.DELETE", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.DELETE() file not found: {path}");
            }
            File.Delete(resolvedPath);
            return OslangValue.Null;
        });

        extensions.Register("FILE.DEL", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.DEL", location);
            var path = RequireStringArg(args, 0, "FILE.DEL", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.DEL() file not found: {path}");
            }
            File.Delete(resolvedPath);
            return OslangValue.Null;
        });

        extensions.Register("FILE.COPY", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.COPY", location);
            var source = RequireStringArg(args, 0, "FILE.COPY", location);
            var dest = RequireStringArg(args, 1, "FILE.COPY", location);
            var resolvedSource = ResolvePath(source, extensions);
            var resolvedDest = ResolvePath(dest, extensions);
            if (!File.Exists(resolvedSource))
            {
                throw new OslangRuntimeException(location, $"FILE.COPY() source not found: {source}");
            }
            File.Copy(resolvedSource, resolvedDest);
            return OslangValue.Null;
        });

        extensions.Register("FILE.MOVE", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.MOVE", location);
            var source = RequireStringArg(args, 0, "FILE.MOVE", location);
            var dest = RequireStringArg(args, 1, "FILE.MOVE", location);
            var resolvedSource = ResolvePath(source, extensions);
            var resolvedDest = ResolvePath(dest, extensions);
            if (!File.Exists(resolvedSource))
            {
                throw new OslangRuntimeException(location, $"FILE.MOVE() source not found: {source}");
            }
            File.Move(resolvedSource, resolvedDest);
            return OslangValue.Null;
        });

        extensions.Register("FILE.SIZE", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.SIZE", location);
            var path = RequireStringArg(args, 0, "FILE.SIZE", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.SIZE() file not found: {path}");
            }
            return new NumberValue(new FileInfo(resolvedPath).Length);
        });

        extensions.Register("FILE.EXTENSION", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.EXTENSION", location);
            var path = RequireStringArg(args, 0, "FILE.EXTENSION", location);
            return new StringValue(Path.GetExtension(path));
        });

        extensions.Register("FILE.NAME", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.NAME", location);
            var path = RequireStringArg(args, 0, "FILE.NAME", location);
            return new StringValue(Path.GetFileName(path));
        });

        extensions.Register("FILE.DIR", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.DIR", location);
            var path = RequireStringArg(args, 0, "FILE.DIR", location);
            return new StringValue(Path.GetDirectoryName(path) ?? string.Empty);
        });

        extensions.Register("FILE.OPEN", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.OPEN", location);
            var path = RequireStringArg(args, 0, "FILE.OPEN", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.OPEN() file not found: {path}");
            }
            // Return a placeholder - stream operations would need more infrastructure
            return new StringValue(resolvedPath);
        });

        extensions.Register("DIR.EXISTS", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.EXISTS", location);
            var path = RequireStringArg(args, 0, "DIR.EXISTS", location);
            return BooleanValue.Of(Directory.Exists(ResolvePath(path, extensions)));
        });

        extensions.Register("DIR.CREATE", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.CREATE", location);
            var path = RequireStringArg(args, 0, "DIR.CREATE", location);
            var resolvedPath = ResolvePath(path, extensions);
            Directory.CreateDirectory(resolvedPath);
            return OslangValue.Null;
        });

        extensions.Register("DIR.DELETE", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.DELETE", location);
            var path = RequireStringArg(args, 0, "DIR.DELETE", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!Directory.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"DIR.DELETE() directory not found: {path}");
            }
            Directory.Delete(resolvedPath);
            return OslangValue.Null;
        });

        extensions.Register("DIR.LIST", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.LIST", location);
            var path = RequireStringArg(args, 0, "DIR.LIST", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!Directory.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"DIR.LIST() directory not found: {path}");
            }
            var entries = Directory.GetFileSystemEntries(resolvedPath);
            var items = entries.Select(e => (OslangValue)new StringValue(e)).ToList();
            return new ArrayValue(items, RuntimeType.String);
        });

        extensions.Register("DIR.FILES", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.FILES", location);
            var path = RequireStringArg(args, 0, "DIR.FILES", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!Directory.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"DIR.FILES() directory not found: {path}");
            }
            var files = Directory.GetFiles(resolvedPath);
            var items = files.Select(f => (OslangValue)new StringValue(f)).ToList();
            return new ArrayValue(items, RuntimeType.String);
        });

        extensions.Register("DIR.DIRS", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.DIRS", location);
            var path = RequireStringArg(args, 0, "DIR.DIRS", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!Directory.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"DIR.DIRS() directory not found: {path}");
            }
            var dirs = Directory.GetDirectories(resolvedPath);
            var items = dirs.Select(d => (OslangValue)new StringValue(d)).ToList();
            return new ArrayValue(items, RuntimeType.String);
        });

        extensions.Register("DIR.CURRENT", (args, location) =>
        {
            RequireArgCount(args, 0, "DIR.CURRENT", location);
            return new StringValue(Directory.GetCurrentDirectory());
        });

        extensions.Register("DIR.CHANGE", (args, location) =>
        {
            RequireArgCount(args, 1, "DIR.CHANGE", location);
            var path = RequireStringArg(args, 0, "DIR.CHANGE", location);
            var resolvedPath = ResolvePath(path, extensions);
            Directory.SetCurrentDirectory(resolvedPath);
            return OslangValue.Null;
        });

        extensions.Register("DIR.RENAME", (args, location) =>
        {
            RequireArgCount(args, 2, "DIR.RENAME", location);
            var source = RequireStringArg(args, 0, "DIR.RENAME", location);
            var dest = RequireStringArg(args, 1, "DIR.RENAME", location);
            var resolvedSource = ResolvePath(source, extensions);
            var resolvedDest = ResolvePath(dest, extensions);
            if (!Directory.Exists(resolvedSource))
            {
                throw new OslangRuntimeException(location, $"DIR.RENAME() source not found: {source}");
            }
            Directory.Move(resolvedSource, resolvedDest);
            return OslangValue.Null;
        });

        extensions.Register("DIR.COPY", (args, location) =>
        {
            RequireArgCount(args, 2, "DIR.COPY", location);
            var source = RequireStringArg(args, 0, "DIR.COPY", location);
            var dest = RequireStringArg(args, 1, "DIR.COPY", location);
            var resolvedSource = ResolvePath(source, extensions);
            var resolvedDest = ResolvePath(dest, extensions);
            if (!Directory.Exists(resolvedSource))
            {
                throw new OslangRuntimeException(location, $"DIR.COPY() source not found: {source}");
            }
            CopyDirectory(resolvedSource, resolvedDest);
            return OslangValue.Null;
        });
    }

    private static void RequireArgCount(IReadOnlyList<OslangValue> args, int expected, string fnName, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects {expected} argument(s), got {args.Count}.");
        }
    }

    private static string RequireStringArg(IReadOnlyList<OslangValue> args, int index, string fnName, SourceLocation location)
    {
        if (index >= args.Count)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects at least {index + 1} argument(s).");
        }

        if (args[index] is not StringValue s)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects a STRING argument, got {args[index].TypeName}.");
        }

        return s.Value;
    }

    private static double RequireNumberArg(IReadOnlyList<OslangValue> args, int index, string fnName, SourceLocation location)
    {
        if (index >= args.Count)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects at least {index + 1} argument(s).");
        }

        if (args[index] is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects a NUMBER argument, got {args[index].TypeName}.");
        }

        return n.Value;
    }

    private static string ResolvePath(string path, ExtensionRegistry extensions)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return path;
        }

        if (!string.IsNullOrEmpty(extensions.BasePath))
        {
            return Path.GetFullPath(Path.Combine(extensions.BasePath, path));
        }

        return Path.GetFullPath(path);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
