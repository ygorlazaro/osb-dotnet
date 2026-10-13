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

/// <summary>
/// OSLANG 0.6 marker for imported standard-library modules (e.g., OSL.I18N).
/// Instances of this type are treated as module proxies by the interpreter.
/// </summary>
public sealed class ModuleValue : OslangValue
{
    public string ModuleName { get; }
    public override RuntimeType Type => RuntimeType.Object;

    public ModuleValue(string moduleName)
    {
        ModuleName = moduleName;
    }
}

public sealed class EnumTypeValue : OslangValue
{
    public string EnumName { get; }
    public override RuntimeType Type => RuntimeType.Object;
    public override string TypeName => EnumName;

    public EnumTypeValue(string enumName)
    {
        EnumName = enumName;
    }
}

// ============================================================
// OSLANG 0.61 - ENUM
// ============================================================

public sealed class EnumValue(OslangValue underlyingValue, string enumTypeName, string memberName) : OslangValue
{
    public OslangValue UnderlyingValue { get; } = underlyingValue;
    public string EnumTypeName { get; } = enumTypeName;
    public string MemberName { get; } = memberName;
    public override RuntimeType Type => RuntimeType.Enum;
    public override string TypeName => EnumTypeName;

    public override bool Equals(object? obj)
    {
        if (obj is not EnumValue other) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(EnumTypeName, other.EnumTypeName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(MemberName, other.MemberName, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EnumTypeName.ToUpperInvariant(), MemberName.ToUpperInvariant());
    }
}

public sealed class EnumSetValue(string enumTypeName, HashSet<EnumValue> values) : OslangValue
{
    public string EnumTypeName { get; } = enumTypeName;
    public HashSet<EnumValue> Values { get; } = values;
    public override RuntimeType Type => RuntimeType.EnumSet;
    public override string TypeName => $"{enumTypeName}[]";
}

public sealed class KeyValue : OslangValue
{
    public EnumValue Key { get; }
    public string? Char { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }
    public override RuntimeType Type => RuntimeType.Object;

    public KeyValue(EnumValue key, string? charValue, bool ctrl, bool alt, bool shift)
    {
        Key = key;
        Char = charValue;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }
}

public sealed class SizeValue : OslangValue
{
    public int Width { get; }
    public int Height { get; }
    public override RuntimeType Type => RuntimeType.Object;

    public SizeValue(int width, int height)
    {
        Width = width;
        Height = height;
    }
}

public sealed class CursorPositionValue : OslangValue
{
    public int Row { get; }
    public int Column { get; }
    public override RuntimeType Type => RuntimeType.Object;

    public CursorPositionValue(int row, int column)
    {
        Row = row;
        Column = column;
    }
}

// ============================================================
// OSLANG 0.5 - DATE / TIME
// ============================================================

public sealed class DateValue(int daysSinceEpoch) : OslangValue
{
    public int DaysSinceEpoch { get; } = daysSinceEpoch;
    public override RuntimeType Type => RuntimeType.Date;
    public override string TypeName => "DATE";

    public int Year
    {
        get
        {
            var (year, _, _) = DaysToYMD(DaysSinceEpoch);
            return year;
        }
    }

    public int Month
    {
        get
        {
            var (_, month, _) = DaysToYMD(DaysSinceEpoch);
            return month;
        }
    }

    public int Day
    {
        get
        {
            var (_, _, day) = DaysToYMD(DaysSinceEpoch);
            return day;
        }
    }

    public static (int year, int month, int day) DaysToYMD(int daysSinceEpoch)
    {
        int doe = daysSinceEpoch + 719468; // days since 0000-03-01
        int era = doe / 146097;
        int doeAdj = doe - era * 146097;
        int yoe = (doeAdj - doeAdj / 1460 + doeAdj / 36524 - doeAdj / 146096) / 365;
        int y = era * 400 + yoe;
        int doy = doeAdj - (365 * yoe + yoe / 4 - yoe / 100);
        int mp = (5 * doy + 2) / 153;
        int day = doy - (153 * mp + 2) / 5 + 1;
        int month = mp + 3;
        int year = era * 400 + yoe;
        if (month > 12) { month -= 12; y++; }
        return (year, month, day);
    }
}

public sealed class TimeValue(long millisSinceMidnight) : OslangValue
{
    public long MillisSinceMidnight { get; } = millisSinceMidnight;
    public override RuntimeType Type => RuntimeType.Time;
    public override string TypeName => "TIME";

    public int Hour => (int)(MillisSinceMidnight / 3600000);
    public int Minute => (int)((MillisSinceMidnight % 3600000) / 60000);
    public int Second => (int)((MillisSinceMidnight % 60000) / 1000);
    public int Millisecond => (int)(MillisSinceMidnight % 1000);
}
