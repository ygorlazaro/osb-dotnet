using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

/// <summary>
/// Dispatches method calls on primitive OSLANG values: STRING, NUMBER, BOOLEAN, ARRAY.
/// Also handles MATH, FILE, and DIR namespace method calls.
/// </summary>
public static class PrimitiveMethodDispatcher
{
    public static OslangValue Dispatch(OslangValue receiver, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (receiver is StringValue s)
        {
            return DispatchString(s, methodName, args, location);
        }

        if (receiver is NumberValue n)
        {
            return DispatchNumber(n, methodName, args, location);
        }

        if (receiver is BooleanValue b)
        {
            return DispatchBoolean(b, methodName, args, location);
        }

        if (receiver is ArrayValue a)
        {
            return DispatchArray(a, methodName, args, location);
        }

        throw new OslangRuntimeException(location, $"Cannot call method '{methodName}' on type {receiver.TypeName}.");
    }

    private static OslangValue DispatchString(StringValue s, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "TOUPPER":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(s.Value.ToUpperInvariant());
            case "NORMALIZE":
                EnsureArgCount(args, 0, methodName, location);
                return NormalizeString(s.Value, location);
            case "TOLOWER":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(s.Value.ToLowerInvariant());
            case "TRIM":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(s.Value.Trim());
            case "STARTSWITH":
                EnsureArgCount(args, 1, methodName, location);
                return BooleanValue.Of(s.Value.StartsWith(RequireString(args, 0, methodName, location)));
            case "ENDSWITH":
                EnsureArgCount(args, 1, methodName, location);
                return BooleanValue.Of(s.Value.EndsWith(RequireString(args, 0, methodName, location)));
            case "CONTAINS":
                EnsureArgCount(args, 1, methodName, location);
                return BooleanValue.Of(s.Value.Contains(RequireString(args, 0, methodName, location)));
            case "INDEXOF":
                EnsureArgCount(args, 1, methodName, location);
                var index = s.Value.IndexOf(RequireString(args, 0, methodName, location));
                return new NumberValue(index >= 0 ? index : -1);
            case "SUBSTR":
                EnsureArgCount(args, 2, methodName, location);
                var start = (int)RequireNumber(args, 0, methodName, location);
                var length = (int)RequireNumber(args, 1, methodName, location);
                if (start < 0 || length < 0 || start + length > s.Value.Length)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() arguments out of range.");
                }
                return new StringValue(s.Value.Substring(start, length));
            case "REPLACE":
                EnsureArgCount(args, 2, methodName, location);
                return new StringValue(s.Value.Replace(RequireString(args, 0, methodName, location), RequireString(args, 1, methodName, location)));
            case "SPLIT":
                EnsureArgCount(args, 1, methodName, location);
                var parts = s.Value.Split(RequireString(args, 0, methodName, location));
                var items = parts.Select(p => (OslangValue)new StringValue(p)).ToList();
                return new ArrayValue(items, RuntimeType.String);
            case "COUNT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(s.Value.Length);
            case "ISEMPTY":
                EnsureArgCount(args, 0, methodName, location);
                return BooleanValue.Of(s.Value.Length == 0);
            case "REVERSE":
                EnsureArgCount(args, 0, methodName, location);
                var chars = s.Value.ToCharArray();
                Array.Reverse(chars);
                return new StringValue(new string(chars));
            case "TOSTRING":
                EnsureArgCount(args, 0, methodName, location);
                return s;
            case "PADSTART":
                return DispatchPadStart(s, args, methodName, location);
            case "PADEND":
                return DispatchPadEnd(s, args, methodName, location);
            case "REPEAT":
                return DispatchRepeat(s, args, methodName, location);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on STRING.");
        }
    }

    private static OslangValue DispatchNumber(NumberValue n, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "TOSTRING":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            case "ABS":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(Math.Abs(n.Value));
            case "FLOOR":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(Math.Floor(n.Value));
            case "CEIL":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(Math.Ceiling(n.Value));
            case "ISINTEGER":
                EnsureArgCount(args, 0, methodName, location);
                return BooleanValue.Of(n.Value == Math.Floor(n.Value));
            case "BETWEEN":
                EnsureArgCount(args, 2, methodName, location);
                var min = RequireNumber(args, 0, methodName, location);
                var max = RequireNumber(args, 1, methodName, location);
                return BooleanValue.Of(n.Value >= min && n.Value <= max);
            case "TRUNC":
                return DispatchTrunc(n, args, methodName, location);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on NUMBER.");
        }
    }

    private static OslangValue DispatchBoolean(BooleanValue b, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "TOSTRING":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(b.Value ? "TRUE" : "FALSE");
            case "TOGGLE":
                EnsureArgCount(args, 0, methodName, location);
                return BooleanValue.Of(!b.Value);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on BOOLEAN.");
        }
    }

    private static OslangValue DispatchArray(ArrayValue a, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "COUNT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(a.Items.Count);
            case "FIRST":
                EnsureArgCount(args, 0, methodName, location);
                if (a.Items.Count == 0) throw new OslangRuntimeException(location, "FIRST() on empty array.");
                return a.Items[0];
            case "LAST":
                EnsureArgCount(args, 0, methodName, location);
                if (a.Items.Count == 0) throw new OslangRuntimeException(location, "LAST() on empty array.");
                return a.Items[^1];
            case "CONTAINS":
                EnsureArgCount(args, 1, methodName, location);
                return BooleanValue.Of(a.Items.Any(item => ValuesEqual(item, args[0])));
            case "INDEXOF":
                EnsureArgCount(args, 1, methodName, location);
                for (var i = 0; i < a.Items.Count; i++)
                {
                    if (ValuesEqual(a.Items[i], args[0])) return new NumberValue(i);
                }
                return new NumberValue(-1);
            case "ADD":
                EnsureArgCount(args, 1, methodName, location);
                a.Items.Add(args[0]);
                return a;
            case "REMOVE":
                EnsureArgCount(args, 1, methodName, location);
                for (var i = a.Items.Count - 1; i >= 0; i--)
                {
                    if (ValuesEqual(a.Items[i], args[0]))
                    {
                        a.Items.RemoveAt(i);
                        break;
                    }
                }
                return a;
            case "CLEAR":
                EnsureArgCount(args, 0, methodName, location);
                a.Items.Clear();
                a.ElementType = null;
                return a;
            case "REVERSE":
                EnsureArgCount(args, 0, methodName, location);
                a.Items.Reverse();
                return a;
            case "SORT":
                EnsureArgCount(args, 0, methodName, location);
                a.Items.Sort((x, y) => CompareValues(x, y));
                return a;
            case "JOIN":
                EnsureArgCount(args, 1, methodName, location);
                var separator = RequireString(args, 0, methodName, location);
                return new StringValue(string.Join(separator, a.Items.Select(item => Conversions.ToDisplayString(item, location))));
            case "PUSH":
                EnsureArgCount(args, 1, methodName, location);
                a.Items.Add(args[0]);
                if (a.ElementType is null && args[0].Type != RuntimeType.Null)
                {
                    a.ElementType = args[0].Type;
                }
                return a;
            case "POP":
                EnsureArgCount(args, 0, methodName, location);
                if (a.Items.Count == 0)
                {
                    throw new OslangRuntimeException(location, "POP() on empty array.");
                }
                var last = a.Items[^1];
                a.Items.RemoveAt(a.Items.Count - 1);
                return last;
            case "FINDINDEX":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not FunctionValue funcFind)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a function argument.");
                }
                for (var i = 0; i < a.Items.Count; i++)
                {
                    var result = CallFunctionReference(funcFind, [a.Items[i]], location);
                    if (result is BooleanValue bFind && bFind.Value)
                    {
                        return new NumberValue(i);
                    }
                }
                return new NumberValue(-1);
            case "FOREACH":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not FunctionValue funcForeach)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a function argument.");
                }
                foreach (var item in a.Items)
                {
                    CallFunctionReference(funcForeach, [item], location);
                }
                return OslangValue.Null;
            case "FLAT":
                EnsureArgCount(args, 0, methodName, location);
                var flatItems = new List<OslangValue>();
                foreach (var item in a.Items)
                {
                    if (item is ArrayValue innerArray)
                    {
                        flatItems.AddRange(innerArray.Items);
                    }
                    else
                    {
                        flatItems.Add(item);
                    }
                }
                return new ArrayValue(flatItems, null);
            case "FLATMAP":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not FunctionValue funcFlatMap)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a function argument.");
                }
                var flatMapResult = new List<OslangValue>();
                foreach (var item in a.Items)
                {
                    var mapped = CallFunctionReference(funcFlatMap, [item], location);
                    if (mapped is ArrayValue mappedArray)
                    {
                        flatMapResult.AddRange(mappedArray.Items);
                    }
                    else
                    {
                        flatMapResult.Add(mapped);
                    }
                }
                return new ArrayValue(flatMapResult, null);
            case "MAP":
            case "FILTER":
            case "ANY":
            case "SOME":
            case "ALL":
            case "REDUCE":
                return DispatchArrayFunctional(a, upper, args, location);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on ARRAY.");
        }
    }

    private static OslangValue DispatchArrayFunctional(ArrayValue a, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count == 0)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects a function argument.");
        }

        if (args[0] is not FunctionValue func)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects a function argument, got {args[0].TypeName}.");
        }

        switch (methodName)
        {
            case "MAP":
            {
                var result = new List<OslangValue>();
                foreach (var item in a.Items)
                {
                    result.Add(CallFunctionReference(func, [item], location));
                }
                return new ArrayValue(result, null);
            }
            case "FILTER":
            {
                var result = new List<OslangValue>();
                foreach (var item in a.Items)
                {
                    var keep = CallFunctionReference(func, [item], location);
                    if (keep is BooleanValue b && b.Value)
                    {
                        result.Add(item);
                    }
                }
                return new ArrayValue(result, a.ElementType);
            }
            case "ANY":
            case "SOME":
            {
                foreach (var item in a.Items)
                {
                    var result = CallFunctionReference(func, [item], location);
                    if (result is BooleanValue b && b.Value)
                    {
                        return BooleanValue.True;
                    }
                }
                return BooleanValue.False;
            }
            case "ALL":
            {
                foreach (var item in a.Items)
                {
                    var result = CallFunctionReference(func, [item], location);
                    if (result is not BooleanValue b || !b.Value)
                    {
                        return BooleanValue.False;
                    }
                }
                return BooleanValue.True;
            }
            case "REDUCE":
            {
                if (args.Count < 2)
                {
                    throw new OslangRuntimeException(location, "REDUCE() expects an initial value argument.");
                }
                var accumulator = args[1];
                foreach (var item in a.Items)
                {
                    accumulator = CallFunctionReference(func, [accumulator, item], location);
                }
                return accumulator;
            }
            default:
                throw new OslangRuntimeException(location, $"Unknown array method '{methodName}'.");
        }
    }

    private static OslangValue CallFunctionReference(FunctionValue func, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        return func.Callback(args, location);
    }

    private static void EnsureArgCount(IReadOnlyList<OslangValue> args, int expected, string methodName, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects {expected} argument(s), got {args.Count}.");
        }
    }

    private static string RequireString(IReadOnlyList<OslangValue> args, int index, string methodName, SourceLocation location)
    {
        if (index >= args.Count || args[index] is not StringValue s)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument at position {index + 1}.");
        }
        return s.Value;
    }

    private static double RequireNumber(IReadOnlyList<OslangValue> args, int index, string methodName, SourceLocation location)
    {
        if (index >= args.Count || args[index] is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects a NUMBER argument at position {index + 1}.");
        }
        return n.Value;
    }

    private static bool ValuesEqual(OslangValue left, OslangValue right)
    {
        if (left.Type == RuntimeType.Null || right.Type == RuntimeType.Null)
        {
            return left.Type == RuntimeType.Null && right.Type == RuntimeType.Null;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        return (left, right) switch
        {
            (NumberValue a, NumberValue b) => a.Value == b.Value,
            (StringValue a, StringValue b) => a.Value == b.Value,
            (BooleanValue a, BooleanValue b) => a.Value == b.Value,
            (ArrayValue a, ArrayValue b) => ReferenceEquals(a, b),
            (ObjectValue a, ObjectValue b) => ReferenceEquals(a.Instance, b.Instance),
            (EnumValue a, EnumValue b) => a.EnumTypeName == b.EnumTypeName && a.MemberName == b.MemberName,
            (EnumSetValue a, EnumSetValue b) => a.EnumTypeName == b.EnumTypeName && a.Values.SetEquals(b.Values),
            _ => false,
        };
    }

    private static int CompareValues(OslangValue left, OslangValue right)
    {
        if (left is NumberValue l && right is NumberValue r)
        {
            return l.Value.CompareTo(r.Value);
        }

        if (left is StringValue sl && right is StringValue sr)
        {
            return string.Compare(sl.Value, sr.Value, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Cannot compare {left.TypeName} and {right.TypeName}.");
    }

    private static StringValue NormalizeString(string value, SourceLocation location)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder();
        foreach (var ch in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }
        var result = builder.ToString().Normalize(System.Text.NormalizationForm.FormC)
            .Replace('Ç', 'C')
            .Replace('ç', 'C');
        return new StringValue(result.ToUpperInvariant());
    }

    private static StringValue DispatchPadStart(StringValue s, IReadOnlyList<OslangValue> args, string methodName, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects 1 or 2 arguments, got {args.Count}.");
        }
        var width = (int)RequireNumber(args, 0, methodName, location);
        var padding = args.Count >= 2 ? RequireString(args, 1, methodName, location) : " ";
        if (width < 0)
        {
            throw new OslangRuntimeException(location, $"{methodName}() width must be non-negative.");
        }
        if (s.Value.Length >= width)
        {
            return s;
        }
        var padLength = width - s.Value.Length;
        var pad = new string(padding[0], padLength);
        return new StringValue(pad + s.Value);
    }

    private static StringValue DispatchPadEnd(StringValue s, IReadOnlyList<OslangValue> args, string methodName, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects 1 or 2 arguments, got {args.Count}.");
        }
        var width = (int)RequireNumber(args, 0, methodName, location);
        var padding = args.Count >= 2 ? RequireString(args, 1, methodName, location) : " ";
        if (width < 0)
        {
            throw new OslangRuntimeException(location, $"{methodName}() width must be non-negative.");
        }
        if (s.Value.Length >= width)
        {
            return s;
        }
        var padLength = width - s.Value.Length;
        var pad = new string(padding[0], padLength);
        return new StringValue(s.Value + pad);
    }

    private static StringValue DispatchRepeat(StringValue s, IReadOnlyList<OslangValue> args, string methodName, SourceLocation location)
    {
        EnsureArgCount(args, 1, methodName, location);
        var count = (int)RequireNumber(args, 0, methodName, location);
        if (count < 0)
        {
            throw new OslangRuntimeException(location, $"{methodName}() count must be non-negative.");
        }
        return new StringValue(string.Concat(Enumerable.Repeat(s.Value, count)));
    }

    private static OslangValue DispatchTrunc(NumberValue n, IReadOnlyList<OslangValue> args, string methodName, SourceLocation location)
    {
        if (args.Count == 0)
        {
            return new NumberValue(Math.Truncate(n.Value));
        }
        if (args.Count == 1)
        {
            var decimals = (int)RequireNumber(args, 0, methodName, location);
            if (decimals < 0)
            {
                throw new OslangRuntimeException(location, $"{methodName}() decimals must be non-negative.");
            }
            var factor = Math.Pow(10, decimals);
            return new NumberValue(Math.Truncate(n.Value * factor) / factor);
        }
        throw new OslangRuntimeException(location, $"{methodName}() expects 0 or 1 arguments, got {args.Count}.");
    }
}
