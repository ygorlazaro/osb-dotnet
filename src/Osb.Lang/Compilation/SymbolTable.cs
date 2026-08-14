using Osb.Lang.Ast;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// Global symbol table for a compilation unit.
/// </summary>
public sealed class SymbolTable
{
    public Dictionary<string, FunctionOverloadSet> Functions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ClassDefinition> Classes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, InterfaceDefinition> Interfaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, EventSymbol> Events { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Module> Modules { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<OslangDiagnostic> Diagnostics { get; } = new();
    public FunctionDecl? MainFunction { get; private set; }

    public FunctionOverloadSet GetOrCreateFunction(string name)
    {
        if (!Functions.TryGetValue(name, out var set))
        {
            set = new FunctionOverloadSet(name);
            Functions[name] = set;
        }

        return set;
    }

    public void SetMain(FunctionDecl main)
    {
        if (MainFunction is not null)
        {
            Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, main.Location, $"Duplicate FUNCTION MAIN(). Entry point already declared."));
            return;
        }

        MainFunction = main;
    }
}
