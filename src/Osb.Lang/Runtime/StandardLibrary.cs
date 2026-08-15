using System.Globalization;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.4 standard library. Global functions: STR, NUMBER, BOOL, COUNT, TYPEOF.
/// Math functions moved to MATH namespace (PrimitiveMethodDispatcher.MathNamespace).
/// </summary>
public static class StandardLibrary
{
    public static readonly IReadOnlyCollection<string> FunctionNames =
        ["STR", "NUMBER", "BOOL", "COUNT", "TYPEOF"];

    public static OslangValue Call(string name, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        return name switch
        {
            "STR" => Str(One(args, name, location), location),
            "NUMBER" => NumberOf(One(args, name, location), location),
            "BOOL" => BooleanValue.Of(Conversions.IsTruthy(One(args, name, location))),
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

    private static OslangValue Count(OslangValue value, SourceLocation location) => value switch
    {
        StringValue s => new NumberValue(s.Value.Length),
        ArrayValue a => new NumberValue(a.Items.Count),
        NullValue => new NumberValue(0),
        _ => throw new OslangRuntimeException(location, $"COUNT() is not supported for type {value.TypeName}."),
    };
}
