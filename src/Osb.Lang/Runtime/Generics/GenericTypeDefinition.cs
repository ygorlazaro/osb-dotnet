using Osb.Lang.Ast;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime.Generics;

/// <summary>
/// Represents a generic type definition (e.g., BOX<T>).
/// </summary>
public sealed class GenericTypeDefinition
{
    public string Name { get; }
    public IReadOnlyList<string> GenericParameters { get; }
    public IReadOnlyList<PropertyDefinition> Properties { get; }
    public IReadOnlyList<MethodDefinition> Methods { get; }
    public ConstructorDefinition? Constructor { get; }

    public GenericTypeDefinition(string name, IReadOnlyList<string> genericParameters, IReadOnlyList<PropertyDefinition> properties, IReadOnlyList<MethodDefinition> methods, ConstructorDefinition? constructor)
    {
        Name = name;
        GenericParameters = genericParameters;
        Properties = properties;
        Methods = methods;
        Constructor = constructor;
    }

    public ConstructedType Construct(IReadOnlyList<RuntimeType> typeArguments)
    {
        if (typeArguments.Count != GenericParameters.Count)
        {
            throw new ArgumentException($"Generic type '{Name}' expects {GenericParameters.Count} type argument(s), got {typeArguments.Count}.");
        }

        return new ConstructedType(this, typeArguments);
    }
}
