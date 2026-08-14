using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// Validação semântica de OSLANG 0.2: classes, interfaces, herança e contratos.
/// </summary>
public static class SemanticAnalyzer
{
    public static void Validate(
        OslangProgram program,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, InterfaceDefinition> interfaces)
    {
        foreach (var cls in classes.Values)
        {
            ValidateClass(cls, classes, interfaces);
        }
    }

    private static void ValidateClass(
        ClassDefinition classDef,
        IReadOnlyDictionary<string, ClassDefinition> allClasses,
        IReadOnlyDictionary<string, InterfaceDefinition> allInterfaces)
    {
        var implementedMethods = new Dictionary<string, MethodDefinition>(StringComparer.OrdinalIgnoreCase);
        var implementedProperties = new Dictionary<string, PropertyDefinition>(StringComparer.OrdinalIgnoreCase);

        CollectMembers(classDef, implementedMethods, implementedProperties);

        foreach (var iface in classDef.Interfaces)
        {
            foreach (var member in iface.Members)
            {
                switch (member)
                {
                    case PropertyDecl p:
                        if (!implementedProperties.TryGetValue(p.Name, out var prop))
                        {
                            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' does not implement interface '{iface.Name}'. Missing property: {p.Name}");
                        }
                        if (prop.Visibility != Visibility.Public)
                        {
                            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' implements interface '{iface.Name}' with non-public property '{p.Name}'. Interface members must be PUBLIC.");
                        }
                        break;
                    case MethodDecl m:
                        if (!implementedMethods.TryGetValue(m.Name, out var method))
                        {
                            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' does not implement interface '{iface.Name}'. Missing method: {m.Name}");
                        }
                        if (method.Visibility != Visibility.Public)
                        {
                            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' implements interface '{iface.Name}' with non-public method '{m.Name}'. Interface members must be PUBLIC.");
                        }
                        if (!SignaturesMatch(m, method))
                        {
                            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' implements interface '{iface.Name}' with incompatible signature for method '{m.Name}'.");
                        }
                        break;
                }
            }
        }
    }

    private static void CollectMembers(
        ClassDefinition classDef,
        Dictionary<string, MethodDefinition> methods,
        Dictionary<string, PropertyDefinition> properties)
    {
        foreach (var method in classDef.Methods)
        {
            methods[method.Name] = method;
        }
        foreach (var prop in classDef.Properties)
        {
            properties[prop.Name] = prop;
        }
        if (classDef.BaseClass is not null)
        {
            CollectMembers(classDef.BaseClass, methods, properties);
        }
    }

    private static bool SignaturesMatch(MethodDecl interfaceMethod, MethodDefinition classMethod)
    {
        if (interfaceMethod.Parameters.Count != classMethod.Parameters.Count)
        {
            return false;
        }
        return true;
    }
}
