using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

// ============================================================
// Definições de classe e interface
// ============================================================

public sealed class ClassDefinition
{
    public string Name { get; }
    public ClassDefinition? BaseClass { get; internal set; }
    public List<InterfaceDefinition> Interfaces { get; } = new();
    public IReadOnlyList<InterfaceDefinition> InterfacesReadOnly => Interfaces;
    public IReadOnlyList<PropertyDefinition> Properties { get; }
    public IReadOnlyList<MethodDefinition> Methods { get; }
    public ConstructorDefinition? Constructor { get; }

    public ClassDefinition(
        string name,
        ClassDefinition? baseClass,
        IReadOnlyList<InterfaceDefinition> interfaces,
        IReadOnlyList<PropertyDefinition> properties,
        IReadOnlyList<MethodDefinition> methods,
        ConstructorDefinition? constructor)
    {
        Name = name;
        BaseClass = baseClass;
        Interfaces = new List<InterfaceDefinition>(interfaces);
        Properties = properties;
        Methods = methods;
        Constructor = constructor;
    }

    public MethodDefinition? FindMethod(string name)
    {
        var current = (ClassDefinition?)this;
        while (current != null)
        {
            var method = current.Methods.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (method != null)
            {
                return method;
            }
            current = current.BaseClass;
        }
        return null;
    }

    public PropertyDefinition? FindProperty(string name)
    {
        var current = (ClassDefinition?)this;
        while (current != null)
        {
            var prop = current.Properties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (prop != null)
            {
                return prop;
            }
            current = current.BaseClass;
        }
        return null;
    }
}

public sealed class InterfaceDefinition
{
    public string Name { get; }
    public IReadOnlyList<MemberDecl> Members { get; }

    public InterfaceDefinition(string name, IReadOnlyList<MemberDecl> members)
    {
        Name = name;
        Members = members;
    }
}

public sealed class PropertyDefinition
{
    public string Name { get; }
    public string? TypeName { get; }
    public Visibility Visibility { get; }

    public PropertyDefinition(string name, string? typeName, Visibility visibility)
    {
        Name = name;
        TypeName = typeName;
        Visibility = visibility;
    }
}

public sealed class MethodDefinition
{
    public string Name { get; }
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public IReadOnlyList<Stmt> Body { get; }
    public Visibility Visibility { get; }
    public SourceLocation Location { get; }

    public MethodDefinition(string name, IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<Stmt> body, Visibility visibility, SourceLocation location)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
        Visibility = visibility;
        Location = location;
    }
}

public sealed class ConstructorDefinition
{
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public IReadOnlyList<Stmt> Body { get; }
    public SourceLocation Location { get; }

    public ConstructorDefinition(IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<Stmt> body, SourceLocation location)
    {
        Parameters = parameters;
        Body = body;
        Location = location;
    }
}

// ============================================================
// Instância de objeto
// ============================================================

public sealed class ObjectInstance
{
    public ClassDefinition ClassDefinition { get; }
    public Dictionary<string, OslangValue> PropertyValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ObjectInstance(ClassDefinition classDefinition)
    {
        ClassDefinition = classDefinition;
    }

    public string ClassName => ClassDefinition.Name;
}

// ============================================================
// Valor de objeto (wrapper para expor ao runtime)
// ============================================================

public sealed class ObjectValue : OslangValue
{
    public ObjectInstance Instance { get; }

    public ObjectValue(ObjectInstance instance)
    {
        Instance = instance;
    }

    public override RuntimeType Type => RuntimeType.Object;

    public string ClassName => Instance.ClassName;

    public override string TypeName => ClassName;
}
