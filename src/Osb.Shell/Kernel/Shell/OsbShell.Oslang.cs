using Osb.Lang;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;

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
            Console.WriteLine(I18nService.Get("osl.usage"));
            return;
        }

        RunOslScript(PathResolver.Resolve(args.Trim()), []);
    }

    private void RunOslScript(string scriptPath, IReadOnlyList<string> commandArgs)
    {
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine(I18nService.Get("fs.file_not_found", scriptPath));
            return;
        }

        string source;
        try
        {
            source = File.ReadAllText(scriptPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18nService.Get("fs.cannot_read_file", ex.Message));
            return;
        }

        var extensions = new ExtensionRegistry();
        var consoleHost = new ConsoleHost(extensions);
        extensions.ConsoleHost = consoleHost;
        RegisterOsbShellExtensions(extensions, _env);
        extensions.ForegroundColor = _env.Config.ForeColor;
        extensions.BackgroundColor = _env.Config.BackColor;
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
        catch (AppExitException exit)
        {
            consoleHost.Restore();
            Environment.Exit(exit.ExitCode);
        }
        catch (Exception ex)
        {
            consoleHost.Restore();
            Console.WriteLine(I18nService.Get("osl.unexpected_error", ex.Message));
        }
        finally
        {
            consoleHost.Restore();
        }
    }

    private bool TryRunOslCommand(string commandName, string args)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "APPS", commandName.ToUpperInvariant(), "main.osl");
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
            Console.WriteLine(I18nService.Get("osl.run_usage"));
            return;
        }

        var resolvedPath = PathResolver.Resolve(path.Trim());

        if (!File.Exists(resolvedPath))
        {
            Console.WriteLine(I18nService.Get("fs.file_not_found", path));
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
                RunOslScript(Path.Combine(AppContext.BaseDirectory, "APPS", "KISS", "main.osl"), [candidate]);
                return;
            }
        }

        Console.WriteLine(I18nService.Get("osl.spec_not_found"));
        Console.WriteLine(I18nService.Get("osl.spec_hint"));
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
    private void RegisterOsbShellExtensions(ExtensionRegistry extensions, OsbEnvironment env)
    {
        extensions.Register("FOREGROUND_COLOR", (args, location) =>
        {
            RequireArgCount(args, 0, "FOREGROUND_COLOR", location);
            return new NumberValue(extensions.ForegroundColor);
        });

        extensions.Register("BACKGROUND_COLOR", (args, location) =>
        {
            RequireArgCount(args, 0, "BACKGROUND_COLOR", location);
            return new NumberValue(extensions.BackgroundColor);
        });

        extensions.Register("PWD", (args, location) =>
        {
            RequireArgCount(args, 0, "PWD", location);
            return new StringValue(Directory.GetCurrentDirectory());
        });

        extensions.Register("CFG_GET", (args, location) =>
        {
            RequireArgCount(args, 1, "CFG_GET", location);
            var key = RequireStringArg(args, 0, "CFG_GET", location).ToUpperInvariant();
            var cfg = env.Config;
            return key switch
            {
                "FORECOLOR" => new NumberValue(cfg.ForeColor),
                "BACKCOLOR" => new NumberValue(cfg.BackColor),
                "FOCUSCOLOR" => new NumberValue(cfg.FocusColor),
                "MESSAGE" => new StringValue(cfg.Message),
                _ => throw new OslangRuntimeException(location, $"Unknown config key '{key}'."),
            };
        });

        extensions.Register("CFG_SET", (args, location) =>
        {
            RequireArgCount(args, 2, "CFG_SET", location);
            var key = RequireStringArg(args, 0, "CFG_SET", location).ToUpperInvariant();
            var value = args[1];
            var cfg = env.Config;
            switch (key)
            {
                case "FORECOLOR":
                    cfg.ForeColor = value is NumberValue nf ? (int)nf.Value : throw new OslangRuntimeException(location, "CFG_SET FORECOLOR expects a NUMBER.");
                    break;
                case "BACKCOLOR":
                    cfg.BackColor = value is NumberValue nb ? (int)nb.Value : throw new OslangRuntimeException(location, "CFG_SET BACKCOLOR expects a NUMBER.");
                    break;
                case "FOCUSCOLOR":
                    cfg.FocusColor = value is NumberValue nfc ? (int)nfc.Value : throw new OslangRuntimeException(location, "CFG_SET FOCUSCOLOR expects a NUMBER.");
                    break;
                case "MESSAGE":
                    cfg.Message = value is StringValue sv ? sv.Value : value.ToString();
                    break;
                default:
                    throw new OslangRuntimeException(location, $"Unknown config key '{key}'.");
            }
            return OslangValue.Null;
        });

        extensions.Register("CFG_GET_PROMPT", (args, location) =>
        {
            RequireArgCount(args, 0, "CFG_GET_PROMPT", location);
            return new StringValue(env.Prompt.Layout);
        });

        extensions.Register("CFG_SET_PROMPT", (args, location) =>
        {
            RequireArgCount(args, 1, "CFG_SET_PROMPT", location);
            var layout = RequireStringArg(args, 0, "CFG_SET_PROMPT", location);
            env.Prompt.Layout = layout;
            return OslangValue.Null;
        });

        extensions.Register("SETLANGUAGE", (args, location) =>
        {
            RequireArgCount(args, 1, "SETLANGUAGE", location);
            var lang = RequireStringArg(args, 0, "SETLANGUAGE", location).ToUpperInvariant();
            if (lang != "PT-BR" && lang != "EN-US")
            {
                throw new OslangRuntimeException(location, "SETLANGUAGE expects PT-BR or EN-US.");
            }
            I18nService.SetLanguage(lang);
            Environment.SetEnvironmentVariable("LANGUAGE", lang);
            return OslangValue.Null;
        });

        extensions.Register("CFG_SAVE", (args, location) =>
        {
            RequireArgCount(args, 0, "CFG_SAVE", location);
            env.Config.Save(env.ConfigFile);
            env.Prompt.Save(env.HomeDir);
            return OslangValue.Null;
        });

        extensions.Register("NOW", (args, location) =>
        {
            RequireArgCount(args, 0, "NOW", location);
            var now = DateTime.Now;
            return new ArrayValue([
                new NumberValue(now.Year),
                new NumberValue(now.Month),
                new NumberValue(now.Day),
                new NumberValue(now.Hour),
                new NumberValue(now.Minute),
                new NumberValue(now.Second)
            ], RuntimeType.Number);
        });

        extensions.Register("MONTHNAME", (args, location) =>
        {
            RequireArgCount(args, 1, "MONTHNAME", location);
            var month = (int)RequireNumberArg(args, 0, "MONTHNAME", location);
            var names = new[] {"Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"};
            if (month < 1 || month > 12)
                throw new OslangRuntimeException(location, $"MONTHNAME() month must be 1-12");
            return new StringValue(names[month - 1]);
        });

        extensions.Register("WEEKDAYNAME", (args, location) =>
        {
            RequireArgCount(args, 1, "WEEKDAYNAME", location);
            var weekday = (int)RequireNumberArg(args, 0, "WEEKDAYNAME", location);
            var names = new[] {"Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"};
            if (weekday < 0 || weekday > 6)
                throw new OslangRuntimeException(location, $"WEEKDAYNAME() weekday must be 0-6");
            return new StringValue(names[weekday]);
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

        extensions.Register("I18N", (args, location) =>
        {
            if (args.Count < 1)
            {
                throw new OslangRuntimeException(location, "I18N() expects at least 1 argument (key).");
            }

            var key = RequireStringArg(args, 0, "I18N", location);
            var fallback = args.Count > 1 ? RequireStringArg(args, 1, "I18N", location) : key;

            var language = I18nService.CurrentLanguage;
            var basePath = extensions.BasePath ?? string.Empty;
            var i18nDir = Path.Combine(basePath, "I18N");
            var i18nFile = Path.Combine(i18nDir, language + ".i18n");

            if (!File.Exists(i18nFile))
            {
                i18nFile = Path.Combine(i18nDir, "EN-US.i18n");
            }

            var template = fallback;
            if (File.Exists(i18nFile))
            {
                template = LookupTranslation(i18nFile, key, fallback);
            }

            if (template == fallback)
            {
                var shellI18nFile = Path.Combine(AppContext.BaseDirectory, "I18N", language + ".i18n");
                if (!File.Exists(shellI18nFile))
                {
                    shellI18nFile = Path.Combine(AppContext.BaseDirectory, "I18N", "EN-US.i18n");
                }

                if (File.Exists(shellI18nFile))
                {
                    template = LookupTranslation(shellI18nFile, key, fallback);
                }
            }

            if (args.Count > 1)
            {
                var formatArgs = args.Skip(1)
                    .Select(a => a is StringValue sv ? sv.Value : a.ToString())
                    .ToArray();
                try
                {
                    template = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, formatArgs);
                }
                catch
                {
                    // ignore format errors, return raw template
                }
            }

            return new StringValue(template);
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

        extensions.Register("FILE.READLINES", (args, location) =>
        {
            RequireArgCount(args, 1, "FILE.READLINES", location);
            var path = RequireStringArg(args, 0, "FILE.READLINES", location);
            var resolvedPath = ResolvePath(path, extensions);
            if (!File.Exists(resolvedPath))
            {
                throw new OslangRuntimeException(location, $"FILE.READLINES() file not found: {path}");
            }
            var lines = File.ReadAllLines(resolvedPath)
                .Select(l => (OslangValue)new StringValue(l))
                .ToList();
            return new ArrayValue(lines, RuntimeType.String);
        });

        extensions.Register("FILE.WRITELINES", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.WRITELINES", location);
            var path = RequireStringArg(args, 0, "FILE.WRITELINES", location);
            if (args[1] is not ArrayValue linesArray)
            {
                throw new OslangRuntimeException(location, $"FILE.WRITELINES() expects an ARRAY as second argument, got {args[1].TypeName}.");
            }
            var resolvedPath = ResolvePath(path, extensions);
            var lines = linesArray.Items.Select(item => item is StringValue sv ? sv.Value : item.ToString()).ToList();
            File.WriteAllLines(resolvedPath, lines);
            return OslangValue.Null;
        });

        extensions.Register("FILE.RENAME", (args, location) =>
        {
            RequireArgCount(args, 2, "FILE.RENAME", location);
            var source = RequireStringArg(args, 0, "FILE.RENAME", location);
            var dest = RequireStringArg(args, 1, "FILE.RENAME", location);
            var resolvedSource = ResolvePath(source, extensions);
            var resolvedDest = ResolvePath(dest, extensions);
            if (!File.Exists(resolvedSource))
            {
                throw new OslangRuntimeException(location, $"FILE.RENAME() source not found: {source}");
            }
            File.Move(resolvedSource, resolvedDest);
            return OslangValue.Null;
        });

        extensions.Register("APP.EXIT", (args, location) =>
        {
            RequireArgCount(args, 1, "APP.EXIT", location);
            var code = (int)RequireNumberArg(args, 0, "APP.EXIT", location);
            throw new AppExitException(code);
        });

        extensions.Register("APP.HIGHLIGHT", (args, location) =>
        {
            RequireArgCount(args, 2, "APP.HIGHLIGHT", location);
            var line = RequireStringArg(args, 0, "APP.HIGHLIGHT", location);
            var maxWidth = (int)RequireNumberArg(args, 1, "APP.HIGHLIGHT", location);
            return new StringValue(OslangHighlighter.Highlight(line, maxWidth));
        });

        var consoleHost = extensions.ConsoleHost;
        if (consoleHost is not null)
        {
            RegisterConsoleExtensions(extensions, consoleHost);
        }
    }

    private static void RegisterConsoleExtensions(ExtensionRegistry extensions, object consoleHostObj)
    {
        extensions.Register("CONSOLE.WIDTH", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("WIDTH", args, location));
        extensions.Register("CONSOLE.HEIGHT", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("HEIGHT", args, location));
        extensions.Register("CONSOLE.SIZE", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("SIZE", args, location));
        extensions.Register("CONSOLE.RESIZED", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("RESIZED", args, location));
        extensions.Register("CONSOLE.SETCURSOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("SETCURSOR", args, location));
        extensions.Register("CONSOLE.GETCURSOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("GETCURSOR", args, location));
        extensions.Register("CONSOLE.HIDECURSOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("HIDECURSOR", args, location));
        extensions.Register("CONSOLE.SHOWCURSOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("SHOWCURSOR", args, location));
        extensions.Register("CONSOLE.CLEAR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("CLEAR", args, location));
        extensions.Register("CONSOLE.CLEARLINE", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("CLEARLINE", args, location));
        extensions.Register("CONSOLE.CLEARAREA", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("CLEARAREA", args, location));
        extensions.Register("CONSOLE.WRITE", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("WRITE", args, location));
        extensions.Register("CONSOLE.COLOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("COLOR", args, location));
        extensions.Register("CONSOLE.RESETCOLOR", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("RESETCOLOR", args, location));
        extensions.Register("CONSOLE.GETKEY", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("GETKEY", args, location));
        extensions.Register("CONSOLE.READKEY", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("READKEY", args, location));
        extensions.Register("CONSOLE.KEYAVAILABLE", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("KEYAVAILABLE", args, location));
        extensions.Register("CONSOLE.ENTER", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("ENTER", args, location));
        extensions.Register("CONSOLE.EXIT", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("EXIT", args, location));
        extensions.Register("CONSOLE.ALTERNATE", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("ALTERNATE", args, location));
        extensions.Register("CONSOLE.BEGINFRAME", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("BEGINFRAME", args, location));
        extensions.Register("CONSOLE.ENDFRAME", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("ENDFRAME", args, location));
        extensions.Register("CONSOLE.FLUSH", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("FLUSH", args, location));
        extensions.Register("CONSOLE.BEEP", (args, location) => ((ConsoleHost)consoleHostObj).Dispatch("BEEP", args, location));
    }

    private static string LookupTranslation(string filePath, string key, string fallback)
    {
        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 1)
            {
                continue;
            }

            var k = trimmed[..equalsIndex].Trim();
            var v = trimmed[(equalsIndex + 1)..].Trim();
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }

        return fallback;
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
