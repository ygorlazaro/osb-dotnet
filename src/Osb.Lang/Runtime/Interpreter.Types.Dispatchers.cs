using Osb.Lang.Ast;
using Osb.Lang.Compilation;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

internal sealed partial class Interpreter
{
    private static OslangValue DispatchEnum(EnumValue enumValue, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "NAME":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(enumValue.MemberName);
            case "VALUE":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(Conversions.ToDisplayString(enumValue.UnderlyingValue, location));
            case "TOSTRING":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(Conversions.ToDisplayString(enumValue.UnderlyingValue, location));
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on enum type {enumValue.EnumTypeName}.");
        }
    }


    private static OslangValue DispatchEnumSet(EnumSetValue enumSet, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "CONTAINS":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not EnumValue ev)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects an enum value.");
                }
                return BooleanValue.Of(enumSet.Values.Contains(ev));
            case "COUNT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(enumSet.Values.Count);
            case "FOREACH":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not FunctionValue func)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a function argument.");
                }
                foreach (var item in enumSet.Values)
                {
                    func.Callback([item], location);
                }
                return OslangValue.Null;
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on enum set.");
        }
    }


    private static void EnsureArgCount(IReadOnlyList<OslangValue> args, int expected, string methodName, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{methodName}() expects {expected} argument(s), got {args.Count}.");
        }
    }


    private static OslangValue DispatchJsonObject(JsonObjectValue obj, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "KEYS":
                EnsureArgCount(args, 0, methodName, location);
                return new JsonArrayValue(obj.Data.Keys.Select(k => (OslangValue)new StringValue(k)).ToList());
            case "VALUES":
                EnsureArgCount(args, 0, methodName, location);
                return new JsonArrayValue(obj.Data.Values.Select(v => (OslangValue)v).ToList());
            case "CONTAINS":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                return BooleanValue.Of(obj.Data.ContainsKey(sv.Value));
            case "COUNT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(obj.Data.Count);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on JSON object.");
        }
    }


    private static OslangValue DispatchJsonArray(JsonArrayValue arr, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "COUNT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(arr.Items.Count);
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on JSON array.");
        }
    }


    private static OslangValue DispatchXmlNode(XmlNodeValue node, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "NAME":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(node.Name);
            case "VALUE":
                EnsureArgCount(args, 0, methodName, location);
                return new StringValue(node.Value ?? "");
            case "ATTRIBUTES":
                EnsureArgCount(args, 0, methodName, location);
                return new JsonObjectValue(node.Attributes.ToDictionary(kv => kv.Key, kv => (OslangValue)new StringValue(kv.Value)));
            case "CHILDREN":
                EnsureArgCount(args, 0, methodName, location);
                return new JsonArrayValue(node.Children.Select(c => (OslangValue)c).ToList());
            case "CHILD":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                var child = node.Children.FirstOrDefault(c => c.Name.Equals(sv.Value, StringComparison.OrdinalIgnoreCase));
                return child is not null ? (OslangValue)child : OslangValue.Null;
            case "HAS":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv2)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                return BooleanValue.Of(node.Children.Any(c => c.Name.Equals(sv2.Value, StringComparison.OrdinalIgnoreCase)));
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on XML node.");
        }
    }
}
