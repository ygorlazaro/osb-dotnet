using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime.Generics;

/// <summary>
/// Represents a constructed generic type (e.g., BOX<NUMBER>).
/// </summary>
public sealed class ConstructedType
{
    public GenericTypeDefinition Definition { get; }
    public IReadOnlyList<RuntimeType> TypeArguments { get; }

    public ConstructedType(GenericTypeDefinition definition, IReadOnlyList<RuntimeType> typeArguments)
    {
        Definition = definition;
        TypeArguments = typeArguments;
    }

    public override string ToString()
    {
        return $"{Definition.Name}<{string.Join(", ", TypeArguments.Select(t => t.ToString().ToUpperInvariant()))}>";
    }
}
