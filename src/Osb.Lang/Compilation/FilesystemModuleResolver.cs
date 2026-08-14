using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;

namespace Osb.Lang.Compilation;

/// <summary>
/// Default filesystem-based module resolver.
/// </summary>
public sealed class FilesystemModuleResolver : IModuleResolver
{
    private readonly string _rootDirectory;

    public FilesystemModuleResolver(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public async Task<Module?> ResolveAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        var fileName = $"{moduleName}.osl";
        var fullPath = FindFileIgnoreCase(_rootDirectory, fileName);

        if (fullPath is null)
        {
            return null;
        }

        var source = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        return LoadModule(moduleName, fullPath, source);
    }

    private static string? FindFileIgnoreCase(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var entryName = Path.GetFileName(entry);
                if (string.Equals(entryName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }

    private Module LoadModule(string name, string path, string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var usings = program.Usings.ToList();
        SourceLocation? mainLocation = null;

        foreach (var fn in program.Functions)
        {
            if (string.Equals(fn.Name, "MAIN", StringComparison.OrdinalIgnoreCase))
            {
                mainLocation = fn.Location;
                break;
            }
        }

        return new Module(name, path, source, program, usings, mainLocation);
    }
}
