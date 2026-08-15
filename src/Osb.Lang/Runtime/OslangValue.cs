using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// Valor de runtime de OSLANG. Todas as subclasses são imutáveis, exceto
/// <see cref="ArrayValue"/>: arrays são valores de referência mutáveis (indexar e
/// atribuir, Numbers[0] = 99, muda o array compartilhado por qualquer variável que
/// aponte para ele) - decisão de design não coberta explicitamente pela seção 21,
/// mas é o comportamento usual de arrays em linguagens desse porte.
/// </summary>
public abstract class OslangValue
{
    public abstract RuntimeType Type { get; }

    /// <summary>String retornada por TYPEOF() para este valor (seção 43) - coincide com o nome do RuntimeType.</summary>
    public virtual string TypeName => Type.ToString().ToUpperInvariant();

    public static readonly NullValue Null = NullValue.Instance;
}

public sealed class NumberValue(double value) : OslangValue
{
    public double Value { get; } = value;
    public override RuntimeType Type => RuntimeType.Number;
}

public sealed class StringValue(string value) : OslangValue
{
    public string Value { get; } = value;
    public override RuntimeType Type => RuntimeType.String;
}

public sealed class BooleanValue(bool value) : OslangValue
{
    public bool Value { get; } = value;
    public override RuntimeType Type => RuntimeType.Boolean;

    public static readonly BooleanValue True = new(true);
    public static readonly BooleanValue False = new(false);

    public static BooleanValue Of(bool value) => value ? True : False;
}

public sealed class NullValue : OslangValue
{
    public static readonly NullValue Instance = new();
    private NullValue() { }
    public override RuntimeType Type => RuntimeType.Null;
}

/// <summary>
/// Array de OSLANG. <see cref="ElementType"/> é null enquanto o array estiver
/// vazio (nenhum elemento definiu o tipo homogêneo ainda, seção 21); a partir daí
/// segue a mesma regra de estabilidade de tipo das variáveis (seção 11): NULL
/// nunca fixa nem quebra o tipo dos elementos.
/// </summary>
public sealed class ArrayValue(List<OslangValue> items, RuntimeType? elementType) : OslangValue
{
    public List<OslangValue> Items { get; } = items;
    public RuntimeType? ElementType { get; set; } = elementType;
    public override RuntimeType Type => RuntimeType.Array;
}

/// <summary>
/// OSLANG 0.4 function reference, used for callbacks like MAP, FILTER, REDUCE.
/// </summary>
public sealed class FunctionValue(Func<IReadOnlyList<OslangValue>, SourceLocation, OslangValue> callback) : OslangValue
{
    public Func<IReadOnlyList<OslangValue>, SourceLocation, OslangValue> Callback { get; } = callback;
    public override RuntimeType Type => RuntimeType.Function;
}
