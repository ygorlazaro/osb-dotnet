using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.4 MATH namespace: MATH.SQRT, MATH.ABS, MATH.POW, MATH.FLOOR, MATH.CEIL,
/// MATH.RANDOM, MATH.MIN, MATH.MAX, MATH.CLAMP, MATH.SIGN, MATH.ROUND, MATH.TRUNC,
/// MATH.MOD, MATH.SIN, MATH.COS, MATH.TAN, MATH.ASIN, MATH.ACOS, MATH.ATAN,
/// MATH.ATAN2, MATH.LOG, MATH.LOG10, MATH.EXP, plus constants MATH.PI and MATH.E.
/// </summary>
public static class MathNamespace
{
    public static readonly NumberValue PI = new(Math.PI);
    public static readonly NumberValue E = new(Math.E);

    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "PI":
                if (args.Count != 0)
                {
                    throw new OslangRuntimeException(location, "MATH.PI expects no arguments.");
                }
                return PI;
            case "E":
                if (args.Count != 0)
                {
                    throw new OslangRuntimeException(location, "MATH.E expects no arguments.");
                }
                return E;
            case "SQRT":
                return new NumberValue(Math.Sqrt(OneNumber(args, methodName, location)));
            case "ABS":
                return new NumberValue(Math.Abs(OneNumber(args, methodName, location)));
            case "POW":
                return Pow(args, methodName, location);
            case "FLOOR":
                return new NumberValue(Math.Floor(OneNumber(args, methodName, location)));
            case "CEIL":
                return new NumberValue(Math.Ceiling(OneNumber(args, methodName, location)));
            case "RANDOM":
                return Random(args, methodName, location);
            case "MIN":
                return Min(args, methodName, location);
            case "MAX":
                return Max(args, methodName, location);
            case "CLAMP":
                return Clamp(args, methodName, location);
            case "SIGN":
                return new NumberValue(Math.Sign(OneNumber(args, methodName, location)));
            case "ROUND":
                return Round(args, methodName, location);
            case "TRUNC":
                return Trunc(args, methodName, location);
            case "MOD":
                return Mod(args, methodName, location);
            case "SIN":
                return new NumberValue(Math.Sin(OneNumber(args, methodName, location)));
            case "COS":
                return new NumberValue(Math.Cos(OneNumber(args, methodName, location)));
            case "TAN":
                return new NumberValue(Math.Tan(OneNumber(args, methodName, location)));
            case "ASIN":
                return new NumberValue(Math.Asin(OneNumber(args, methodName, location)));
            case "ACOS":
                return new NumberValue(Math.Acos(OneNumber(args, methodName, location)));
            case "ATAN":
                return new NumberValue(Math.Atan(OneNumber(args, methodName, location)));
            case "ATAN2":
                return Atan2(args, methodName, location);
            case "LOG":
                return new NumberValue(Math.Log(OneNumber(args, methodName, location)));
            case "LOG10":
                return new NumberValue(Math.Log10(OneNumber(args, methodName, location)));
            case "EXP":
                return new NumberValue(Math.Exp(OneNumber(args, methodName, location)));
            default:
                throw new OslangRuntimeException(location, $"Unknown MATH function '{methodName}'.");
        }
    }

    private static OslangValue Pow(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue baseValue || args[1] is not NumberValue exponent)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments.");
        }
        return new NumberValue(Math.Pow(baseValue.Value, exponent.Value));
    }

    private static OslangValue Random(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count == 0)
        {
            return new NumberValue(new Random().NextDouble());
        }

        if (args.Count == 2)
        {
            if (args[0] is not NumberValue min || args[1] is not NumberValue max)
            {
                throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments for range.");
            }
            var minVal = (int)min.Value;
            var maxVal = (int)max.Value;
            if (minVal > maxVal)
            {
                throw new OslangRuntimeException(location, $"{fn}() min must be <= max.");
            }
            return new NumberValue(new Random().Next(minVal, maxVal + 1));
        }

        throw new OslangRuntimeException(location, $"{fn}() expects 0 or 2 arguments, got {args.Count}.");
    }

    private static OslangValue Min(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue a || args[1] is not NumberValue b)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments.");
        }
        return new NumberValue(Math.Min(a.Value, b.Value));
    }

    private static OslangValue Max(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue a || args[1] is not NumberValue b)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments.");
        }
        return new NumberValue(Math.Max(a.Value, b.Value));
    }

    private static OslangValue Clamp(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 3)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 3 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue value || args[1] is not NumberValue min || args[2] is not NumberValue max)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects three NUMBER arguments.");
        }
        return new NumberValue(Math.Clamp(value.Value, min.Value, max.Value));
    }

    private static OslangValue Round(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count == 1)
        {
            return new NumberValue(Math.Round(OneNumber(args, fn, location)));
        }
        if (args.Count == 2)
        {
            if (args[0] is not NumberValue value || args[1] is not NumberValue digits)
            {
                throw new OslangRuntimeException(location, $"{fn}() expects NUMBER arguments.");
            }
            return new NumberValue(Math.Round(value.Value, (int)digits.Value));
        }
        throw new OslangRuntimeException(location, $"{fn}() expects 1 or 2 arguments, got {args.Count}.");
    }

    private static OslangValue Mod(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue a || args[1] is not NumberValue b)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments.");
        }
        return new NumberValue(a.Value % b.Value);
    }

    private static OslangValue Trunc(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count == 0)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects 1 or 2 arguments, got 0.");
        }
        if (args[0] is not NumberValue value)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects a NUMBER argument.");
        }
        if (args.Count == 1)
        {
            return new NumberValue(Math.Truncate(value.Value));
        }
        if (args.Count == 2)
        {
            if (args[1] is not NumberValue decimals)
            {
                throw new OslangRuntimeException(location, $"{fn}() expects a NUMBER for decimals.");
            }
            var d = (int)decimals.Value;
            if (d < 0)
            {
                throw new OslangRuntimeException(location, $"{fn}() decimals must be non-negative.");
            }
            var factor = Math.Pow(10, d);
            return new NumberValue(Math.Truncate(value.Value * factor) / factor);
        }
        throw new OslangRuntimeException(location, $"{fn}() expects 1 or 2 arguments, got {args.Count}.");
    }

    private static OslangValue Atan2(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 2 arguments, got {args.Count}.");
        }
        if (args[0] is not NumberValue y || args[1] is not NumberValue x)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects two NUMBER arguments.");
        }
        return new NumberValue(Math.Atan2(y.Value, x.Value));
    }

    private static double OneNumber(IReadOnlyList<OslangValue> args, string fn, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects exactly 1 argument, got {args.Count}.");
        }
        if (args[0] is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects a NUMBER argument, got {args[0].TypeName}.");
        }
        return n.Value;
    }
}