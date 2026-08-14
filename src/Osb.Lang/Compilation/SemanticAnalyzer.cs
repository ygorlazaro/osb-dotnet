using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// Validates semantic rules after type resolution.
/// </summary>
public static class SemanticAnalyzer
{
    public static void Validate(Compilation compilation)
    {
        if (compilation.Symbols.MainFunction is null)
        {
            compilation.Symbols.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, SourceLocation.Unknown, "Program has no FUNCTION MAIN()."));
        }

        foreach (var cls in compilation.Symbols.Classes.Values)
        {
            ValidateClass(cls, compilation.Symbols);
        }
    }

    private static void ValidateClass(ClassDefinition classDef, SymbolTable symbols)
    {
        foreach (var iface in classDef.Interfaces)
        {
            foreach (var member in iface.Members)
            {
                switch (member)
                {
                    case PropertyDecl p:
                        if (!classDef.Properties.Any(prop => string.Equals(prop.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            symbols.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, SourceLocation.Unknown, $"Class '{classDef.Name}' does not implement interface '{iface.Name}'. Missing property: {p.Name}"));
                        }
                        break;
                    case MethodDecl m:
                        if (!classDef.Methods.Any(method => string.Equals(method.Name, m.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            symbols.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, SourceLocation.Unknown, $"Class '{classDef.Name}' does not implement interface '{iface.Name}'. Missing method: {m.Name}"));
                        }
                        break;
                }
            }
        }
    }
}
