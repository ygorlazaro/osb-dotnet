using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// Performs declaration discovery across all modules in a compilation.
/// Does not resolve types or validate semantics.
/// </summary>
public static class SymbolDiscovery
{
    public static SymbolTable Discover(IReadOnlyList<Module> modules, IModuleResolver resolver)
    {
        var table = new SymbolTable();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new Stack<string>();

        foreach (var module in modules)
        {
            table.Modules[module.Name] = module;
            DiscoverModule(module, table, resolver, visited, visiting);
        }

        return table;
    }

    private static void DiscoverModule(Module module, SymbolTable table, IModuleResolver resolver, HashSet<string> visited, Stack<string> visiting)
    {
        if (!visited.Add(module.Name))
        {
            return;
        }

        visiting.Push(module.Name);

        try
        {
            foreach (var usingDecl in module.Usings)
            {
                if (visiting.Contains(usingDecl.ModuleName))
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, usingDecl.Location, $"Circular USING dependency detected: '{usingDecl.ModuleName}'."));
                    continue;
                }

                if (usingDecl.ModuleName.StartsWith("OSL.", StringComparison.OrdinalIgnoreCase) || usingDecl.ModuleName.StartsWith("OSB.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!table.Modules.ContainsKey(usingDecl.ModuleName))
                    {
                        var emptyProgram = new OslangProgram(new List<FunctionDecl>(), new List<ClassDecl>(), new List<InterfaceDecl>(), new List<UsingDecl>(), new List<EventDecl>(), new List<EnumDecl>());
                        table.Modules[usingDecl.ModuleName] = new Module(usingDecl.ModuleName, string.Empty, string.Empty, emptyProgram, new List<UsingDecl>(), null);
                    }
                    continue;
                }

                if (!table.Modules.TryGetValue(usingDecl.ModuleName, out var dep))
                {
                    var resolved = resolver.ResolveAsync(usingDecl.ModuleName).GetAwaiter().GetResult();
                    if (resolved is null)
                    {
                        table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, usingDecl.Location, $"Cannot resolve USING '{usingDecl.ModuleName}'."));
                        continue;
                    }

                    table.Modules[usingDecl.ModuleName] = resolved;
                    dep = resolved;
                    DiscoverModule(dep, table, resolver, visited, visiting);
                }
            }

            foreach (var fn in module.Program.Functions)
            {
                if (string.Equals(fn.Name, "MAIN", StringComparison.OrdinalIgnoreCase))
                {
                    table.SetMain(fn);
                }

                var set = table.GetOrCreateFunction(fn.Name);
                set.Add(fn);
            }

            foreach (var cls in module.Program.Classes)
            {
                if (table.Classes.ContainsKey(cls.Name))
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, cls.Location, $"Duplicate class '{cls.Name}'."));
                    continue;
                }

                var classDef = BuildClassDefinition(cls, table);
                table.Classes[cls.Name] = classDef;
            }

            foreach (var iface in module.Program.Interfaces)
            {
                if (table.Interfaces.ContainsKey(iface.Name))
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, iface.Location, $"Duplicate interface '{iface.Name}'."));
                    continue;
                }

                var interfaceDef = new InterfaceDefinition(iface.Name, iface.Members);
                table.Interfaces[iface.Name] = interfaceDef;
            }

            foreach (var e in module.Program.Enums)
            {
                if (table.Enums.Any(existing => string.Equals(existing.Name, e.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, e.Location, $"Duplicate enum '{e.Name}'."));
                    continue;
                }

                table.Enums.Add(e);
            }
        }
        finally
        {
            visiting.Pop();
        }
    }

    private static ClassDefinition BuildClassDefinition(ClassDecl decl, SymbolTable table)
    {
        var properties = new List<PropertyDefinition>();
        var methods = new List<MethodDefinition>();
        ConstructorDefinition? constructor = null;

        foreach (var member in decl.Members)
        {
            switch (member)
            {
                case PropertyDecl p:
                    properties.Add(new PropertyDefinition(p.Name, p.TypeName, p.Visibility));
                    break;
                case MethodDecl m:
                    methods.Add(new MethodDefinition(m.Name, m.Parameters, m.Body, m.Visibility, m.Location));
                    break;
                case ConstructorDecl c:
                    if (constructor is not null)
                    {
                        table.Diagnostics.Add(new OslangDiagnostic(DiagnosticSeverity.Error, c.Location, $"Duplicate constructor in class '{decl.Name}'."));
                    }
                    else
                    {
                        constructor = new ConstructorDefinition(c.Parameters, c.Body, c.Location);
                    }

                    break;
            }
        }

        return new ClassDefinition(decl.Name, baseClass: null, interfaces: [], properties, methods, constructor);
    }
}
