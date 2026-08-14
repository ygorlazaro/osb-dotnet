using System.Globalization;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// Funções da biblioteca padrão de OSLANG que não têm sintaxe própria: conversão
/// (STR, NUMBER, BOOL - seção 40), matemática (SQRT, ABS, POW, FLOOR, CEIL -
/// seção 41), COUNT (seção 42) e TYPEOF (seção 43).
///
/// Decisões de design não explicitadas na especificação:
/// - NUMBER(TRUE)/NUMBER(FALSE) convertem para 1/0 (convenção comum).
/// - NUMBER(NULL) e NUMBER(array) são erro de runtime (conversão inválida).
/// - STR(NULL) retorna "NULL", STR(array) é erro de runtime (ver Conversions).
/// </summary>
public static class StandardLibrary
{
    public static readonly IReadOnlyCollection<string> FunctionNames =
        ["STR", "NUMBER", "BOOL", "SQRT", "ABS", "POW", "FLOOR", "CEIL", "COUNT", "TYPEOF"];

    public static OslangValue Call(string name, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        return name switch
        {
            "STR" => Str(One(args, name, location), location),
            "NUMBER" => NumberOf(One(args, name, location), location),
            "BOOL" => BooleanValue.Of(Conversions.IsTruthy(One(args, name, location))),
            "SQRT" => Sqrt(OneNumber(args, name, location), location),
            "ABS" => new NumberValue(Math.Abs(OneNumber(args, name, location))),
            "POW" => Pow(args, name, location),
            "FLOOR" => new NumberValue(Math.Floor(OneNumber(args, name, location))),
            "CEIL" => new NumberValue(Math.Ceiling(OneNumber(args, name, location))),
            "COUNT" => Count(One(args, name, location), location),
            "TYPEOF" => new StringValue(One(args, name, location).TypeName),
            _ => throw new OslangRuntimeException(location, $"Unknown standard library function '{name}'."),
        };
    }

    private static OslangValue One(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 1 argument, got {args.Count}.");
        }

        return args[0];
    }

    private static double OneNumber(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        var value = One(args, fn, location);
        if (value is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects a NUMBER argument, got {value.TypeName}.");
        }

        return n.Value;
    }

    private static OslangValue Str(OslangValue value, SourceLocation location) => new StringValue(Conversions.ToDisplayString(value, location));

    private static OslangValue NumberOf(OslangValue value, SourceLocation location)
    {
        switch (value)
        {
            case NumberValue n:
                return n;
            case BooleanValue b:
                return new NumberValue(b.Value ? 1 : 0);
            case StringValue s:
                if (double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return new NumberValue(parsed);
                }

                throw new OslangRuntimeException(location, $"Cannot convert \"{s.Value}\" to NUMBER.");
            default:
                throw new OslangRuntimeException(location, $"Cannot convert {value.TypeName} to NUMBER.");
        }
    }

    private static OslangValue Sqrt(double value, SourceLocation location)
    {
        if (value < 0)
        {
            throw new OslangRuntimeException(location, "SQRT of a negative number is not supported (no complex numbers in OSLANG 0.1).");
        }

        return new NumberValue(Math.Sqrt(value));
    }

    private static OslangValue Pow(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }

        if (args[0] is not NumberValue baseValue || args[1] is not NumberValue exponent)
        {
            throw new OslangRuntimeException(location, "POW() expects two NUMBER arguments.");
        }

        return new NumberValue(Math.Pow(baseValue.Value, exponent.Value));
    }

    private static OslangValue Count(OslangValue value, SourceLocation location) => value switch
    {
        StringValue s => new NumberValue(s.Value.Length),
        ArrayValue a => new NumberValue(a.Items.Count),
        NullValue => new NumberValue(0),
        _ => throw new OslangRuntimeException(location, $"COUNT() is not supported for type {value.TypeName}."),
    };
}
