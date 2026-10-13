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
        ["STR", "NUMBER", "BOOL", "COUNT", "TYPEOF", "LEFT", "RIGHT", "INSERT", "REMOVE", "REPEAT", "TOUPPER", "TOLOWER", "TRIM",
         "DATE", "TIME", "DATE.NOW", "TIME.NOW",
         "DATE.YEAR", "DATE.MONTH", "DATE.DAY",
         "TIME.HOUR", "TIME.MINUTE", "TIME.SECOND",
         "DATE.ADDDAYS", "DATE.DIFFDAYS",
         "TIME.DIFFMILLIS"];

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
            "DATE" => DispatchDateTime("DATE", args, location),
            "TIME" => DispatchDateTime("TIME", args, location),
            "DATE.NOW" => DispatchDateTime("DATE.NOW", args, location),
            "TIME.NOW" => DispatchDateTime("TIME.NOW", args, location),
            "DATE.YEAR" => DispatchDateTime("DATE.YEAR", args, location),
            "DATE.MONTH" => DispatchDateTime("DATE.MONTH", args, location),
            "DATE.DAY" => DispatchDateTime("DATE.DAY", args, location),
            "TIME.HOUR" => DispatchDateTime("TIME.HOUR", args, location),
            "TIME.MINUTE" => DispatchDateTime("TIME.MINUTE", args, location),
            "TIME.SECOND" => DispatchDateTime("TIME.SECOND", args, location),
            "DATE.ADDDAYS" => DispatchDateTime("DATE.ADDDAYS", args, location),
            "DATE.DIFFDAYS" => DispatchDateTime("DATE.DIFFDAYS", args, location),
            "TIME.DIFFMILLIS" => DispatchDateTime("TIME.DIFFMILLIS", args, location),
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
    private static OslangValue DispatchDateTime(string name, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = name.ToUpperInvariant();
        return upper switch
        {
            "DATE.NOW" => new DateValue(0),
            "TIME.NOW" => new TimeValue(0),
            "DATE" => DateFromArgs(args, location),
            "TIME" => TimeFromArgs(args, location),
            "DATE.YEAR" => DateComponent(args, location, c => c.Year),
            "DATE.MONTH" => DateComponent(args, location, c => c.Month),
            "DATE.DAY" => DateComponent(args, location, c => c.Day),
            "TIME.HOUR" => TimeComponent(args, location, t => t.Hour),
            "TIME.MINUTE" => TimeComponent(args, location, t => t.Minute),
            "TIME.SECOND" => TimeComponent(args, location, t => t.Second),
            "DATE.ADDDAYS" => DateAddDays(args, location),
            "DATE.DIFFDAYS" => DateDiffDays(args, location),
            "TIME.DIFFMILLIS" => TimeDiffMillis(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown standard library function '{name}'."),
        };
    }

    private static OslangValue DateFromArgs(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 3 || args[0] is not NumberValue n0 || args[1] is not NumberValue n1 || args[2] is not NumberValue n2)
            throw new OslangRuntimeException(location, "DATE() expects 3 numbers.");
        int year = (int)n0.Value;
        int month = (int)n1.Value;
        int day = (int)n2.Value;
        if (month < 1 || month > 12 || day < 1 || day > 31)
            throw new OslangRuntimeException(location, "Invalid date.");
        return new DateValue(DaysSinceEpochStatic(year, month, day));
    }

    private static int DaysSinceEpochStatic(int year, int month, int day)
    {
        if (month <= 2) { month += 12; year--; }
        int era = year / 400;
        int yoe = year - era * 400;
        int doy = (153 * (month - 3) + 2) / 5 + day - 1;
        int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era * 146097 + doe - 719468;
    }

    private static OslangValue TimeFromArgs(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 3 || args[0] is not NumberValue n0 || args[1] is not NumberValue n1 || args[2] is not NumberValue n2)
            throw new OslangRuntimeException(location, "TIME() expects 3 numbers.");
        int hour = (int)n0.Value;
        int minute = (int)n1.Value;
        int second = (int)n2.Value;
        if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
            throw new OslangRuntimeException(location, "Invalid time.");
        long millis = ((long)hour * 3600 + minute * 60 + second) * 1000;
        return new TimeValue(millis);
    }

    private static OslangValue DateComponent(IReadOnlyList<OslangValue> args, SourceLocation location, Func<DateValue, int> selector)
    {
        if (args.Count < 1 || args[0] is not DateValue d)
            throw new OslangRuntimeException(location, "Expected a DATE value.");
        return new NumberValue(selector(d));
    }

    private static OslangValue TimeComponent(IReadOnlyList<OslangValue> args, SourceLocation location, Func<TimeValue, int> selector)
    {
        if (args.Count < 1 || args[0] is not TimeValue t)
            throw new OslangRuntimeException(location, "Expected a TIME value.");
        return new NumberValue(selector(t));
    }

    private static OslangValue DateAddDays(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 2 || args[0] is not DateValue d || args[1] is not NumberValue n)
            throw new OslangRuntimeException(location, "DATE.ADDDAYS(date, days) expects a DATE and a NUMBER.");
        return new DateValue(d.DaysSinceEpoch + (int)n.Value);
    }

    private static OslangValue DateDiffDays(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 2 || args[0] is not DateValue d1 || args[1] is not DateValue d2)
            throw new OslangRuntimeException(location, "DATE.DIFFDAYS(date1, date2) expects two DATEs.");
        return new NumberValue(d1.DaysSinceEpoch - d2.DaysSinceEpoch);
    }

    private static OslangValue TimeDiffMillis(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 2 || args[0] is not TimeValue t1 || args[1] is not TimeValue t2)
            throw new OslangRuntimeException(location, "TIME.DIFFMILLIS(time1, time2) expects two TIMEs.");
        return new NumberValue(t1.MillisSinceMidnight - t2.MillisSinceMidnight);
    }
}
