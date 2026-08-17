using System.Globalization;
using System.Linq;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG standard library. Global functions: STR, NUMBER, BOOL, COUNT, TYPEOF,
/// LEFT, RIGHT, INSERT, REMOVE, REPEAT, TOUPPER, TOLOWER, TRIM.
/// </summary>
public static class StandardLibrary
{
    public static readonly IReadOnlyCollection<string> FunctionNames =
        ["STR", "NUMBER", "BOOL", "COUNT", "TYPEOF", "LEFT", "RIGHT", "INSERT", "REMOVE", "REPEAT", "TOUPPER", "TOLOWER", "TRIM"];

    public static OslangValue Call(string name, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = name.ToUpperInvariant();
        return upper switch
        {
            "STR" => Str(One(args, name, location), location),
            "NUMBER" => NumberOf(One(args, name, location), location),
            "BOOL" => BooleanValue.Of(Conversions.IsTruthy(One(args, name, location))),
            "COUNT" => Count(One(args, name, location), location),
            "TYPEOF" => new StringValue(One(args, name, location).TypeName),
            "LEFT" => DispatchString("LEFT", args, location),
            "RIGHT" => DispatchString("RIGHT", args, location),
            "INSERT" => DispatchString("INSERT", args, location),
            "REMOVE" => DispatchString("REMOVE", args, location),
            "REPEAT" => DispatchString("REPEAT", args, location),
            "TOUPPER" => DispatchString("TOUPPER", args, location),
            "TOLOWER" => DispatchString("TOLOWER", args, location),
            "TRIM" => DispatchString("TRIM", args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown standard library function '{name}'."),
        };
    }

    private static OslangValue DispatchString(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count == 0)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects at least 1 argument, got 0.");
        }

        if (args[0] is not StringValue s)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects a STRING as first argument.");
        }

        return PrimitiveMethodDispatcher.Dispatch(s, methodName, args.Skip(1).ToList(), location);
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
