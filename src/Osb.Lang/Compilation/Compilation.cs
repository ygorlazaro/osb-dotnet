using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// Represents a complete OSLANG compilation unit.
/// </summary>
public sealed class Compilation
{
    private SymbolTable _symbols = new();
    public SymbolTable Symbols => _symbols;
    public IReadOnlyList<OslangDiagnostic> Diagnostics => _symbols.Diagnostics;

    private Compilation()
    {
    }

    public static Compilation Create(IReadOnlyList<Module> modules, IModuleResolver resolver)
    {
        var compilation = new Compilation();
        compilation._symbols = SymbolDiscovery.Discover(modules, resolver);
        TypeResolver.Resolve(compilation._symbols);
        SemanticAnalyzer.Validate(compilation);
        return compilation;
    }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public OslangValue Execute(TextWriter output, TextReader input, Action? clear, ExtensionRegistry? extensions = null, IReadOnlyList<OslangValue>? args = null)
    {
        if (HasErrors)
        {
            var firstError = Diagnostics.First(d => d.Severity == DiagnosticSeverity.Error);
            throw new SemanticException(firstError.Location, firstError.Message);
        }

        var main = _symbols.MainFunction;
        if (main is null)
        {
            throw new SemanticException(SourceLocation.Unknown, "Program has no FUNCTION MAIN().");
        }

        var allUsings = _symbols.Modules.Values.SelectMany(m => m.Program.Usings).ToList();
        var interpreter = new Interpreter(this, extensions ?? new ExtensionRegistry(), output, input, clear, allUsings);
        return interpreter.Run(args);
    }
}
