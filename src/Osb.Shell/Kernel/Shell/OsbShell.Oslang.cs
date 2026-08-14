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

        var path = PathResolver.Resolve(args.Trim());

        if (!File.Exists(path))
        {
            Console.WriteLine($"Arquivo não encontrado: {args}");
            return;
        }

        string source;
        try
        {
            source = File.ReadAllText(path);
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
            var basePath = Path.GetDirectoryName(path) ?? string.Empty;
            interpreter.Execute(source, Console.Out, Console.In, Console.Clear, basePath);
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

        extensions.Register("DIR", (args, location) =>
        {
            var target = args.Count > 0 ? RequireStringArg(args, 0, "DIR", location) : "";
            ListDirectory(target);
            return OslangValue.Null;
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
}
