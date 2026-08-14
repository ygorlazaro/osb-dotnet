using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// Resolves type references, inheritance chains, and generic types after symbol discovery.
/// </summary>
public static class TypeResolver
{
    public static void Resolve(SymbolTable table)
    {
        ResolveInheritance(table);
        ResolveInterfaces(table);
    }

    private static void ResolveInheritance(SymbolTable table)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new Stack<string>();

        foreach (var cls in table.Classes.Values.ToList())
        {
            ResolveClass(cls, table, resolved, visiting);
        }
    }

    private static void ResolveClass(ClassDefinition classDef, SymbolTable table, HashSet<string> resolved, Stack<string> visiting)
    {
        if (resolved.Contains(classDef.Name))
        {
            return;
        }

        if (visiting.Contains(classDef.Name))
        {
            table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, SourceLocation.Unknown, $"Circular inheritance detected for class '{classDef.Name}'."));
            return;
        }

        visiting.Push(classDef.Name);

        try
        {
            var classDecl = FindClassDecl(classDef.Name, table);
            if (classDecl is null)
            {
                resolved.Add(classDef.Name);
                return;
            }

            var remainingNames = new List<string>(classDecl.InheritedNames);

            if (remainingNames.Count > 0)
            {
                var first = remainingNames[0];
                if (table.Classes.TryGetValue(first, out var baseClass))
                {
                    ResolveClass(baseClass, table, resolved, visiting);
                    classDef.BaseClass = baseClass;
                    remainingNames.RemoveAt(0);
                }
            }

            foreach (var ifaceName in remainingNames)
            {
                if (table.Classes.ContainsKey(ifaceName))
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, classDecl.Location, $"Class '{classDef.Name}' cannot inherit from multiple classes. '{ifaceName}' is a class."));
                    continue;
                }

                if (table.Interfaces.TryGetValue(ifaceName, out var iface))
                {
                    classDef.Interfaces.Add(iface);
                }
                else
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, classDecl.Location, $"Unknown interface '{ifaceName}' in class '{classDef.Name}'."));
                }
            }

            resolved.Add(classDef.Name);
        }
        finally
        {
            visiting.Pop();
        }
    }

    private static void ResolveInterfaces(SymbolTable table)
    {
        foreach (var iface in table.Interfaces.Values)
        {
            // Interfaces are resolved in place
        }
    }

    private static ClassDecl? FindClassDecl(string name, SymbolTable table)
    {
        foreach (var module in table.Modules.Values)
        {
            foreach (var cls in module.Program.Classes)
            {
                if (string.Equals(cls.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return cls;
                }
            }
        }

        return null;
    }
}
