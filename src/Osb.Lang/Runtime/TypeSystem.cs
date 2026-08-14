using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// Implementa a regra central de tipagem dinâmica com estabilidade de OSLANG
/// (seção 11): uma variável não tipada assume o tipo do primeiro valor não-NULL
/// que recebe; a partir daí (ou desde a declaração, se tipada explicitamente),
/// toda atribuição de um tipo diferente é um erro de runtime. NULL nunca
/// estabelece nem quebra o tipo de uma variável (seção 11 e 20).
/// </summary>
public static class TypeSystem
{
    public static RuntimeType ParseTypeName(string typeName) => typeName switch
    {
        "NUMBER" => RuntimeType.Number,
        "STRING" => RuntimeType.String,
        "BOOLEAN" => RuntimeType.Boolean,
        _ => throw new ArgumentException($"Unknown OSLANG type name '{typeName}'.", nameof(typeName)),
    };

    /// <summary>Valor default de uma variável explicitamente tipada (seção 12): "" / 0 / FALSE.</summary>
    public static OslangValue DefaultValueFor(RuntimeType type) => type switch
    {
        RuntimeType.String => new StringValue(string.Empty),
        RuntimeType.Number => new NumberValue(0),
        RuntimeType.Boolean => BooleanValue.False,
        _ => throw new ArgumentException($"Type {type} has no default value.", nameof(type)),
    };

    /// <summary>
    /// Atribui <paramref name="newValue"/> a <paramref name="variable"/>, aplicando a regra de
    /// estabilidade de tipo. <paramref name="describeVariable"/> é usado só para a mensagem de erro.
    /// </summary>
    public static void Assign(Variable variable, OslangValue newValue, SourceLocation location, string describeVariable)
    {
        if (newValue.Type == RuntimeType.Null)
        {
            variable.Value = newValue;
            return;
        }

        if (variable.EstablishedType is null)
        {
            variable.EstablishedType = newValue.Type;
        }
        else if (variable.EstablishedType != newValue.Type)
        {
            throw new OslangRuntimeException(
                location,
                $"Type error: cannot assign {newValue.TypeName} to {describeVariable}, which is {variable.EstablishedType.Value.ToString().ToUpperInvariant()}.");
        }

        variable.Value = newValue;
    }

    /// <summary>Mesma regra de estabilidade de tipo, aplicada a um elemento de array (seção 21).</summary>
    public static void AssignArrayElement(ArrayValue array, int index, OslangValue newValue, SourceLocation location)
    {
        if (newValue.Type != RuntimeType.Null)
        {
            if (array.ElementType is null)
            {
                array.ElementType = newValue.Type;
            }
            else if (array.ElementType != newValue.Type)
            {
                throw new OslangRuntimeException(
                    location,
                    $"Type error: cannot assign {newValue.TypeName} into an ARRAY of {array.ElementType.Value.ToString().ToUpperInvariant()}.");
            }
        }

        array.Items[index] = newValue;
    }
}
