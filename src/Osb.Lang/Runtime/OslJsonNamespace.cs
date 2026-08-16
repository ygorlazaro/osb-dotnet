using System.IO;
using System.Text;
using System.Text.Json;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 OSL.JSON standard library implementation.
/// </summary>
public static class OslJsonNamespace
{
    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        return upper switch
        {
            "PARSE" => Parse(args, location),
            "STRINGIFY" => Stringify(args, location),
            "PRETTY" => Pretty(args, location),
            "READ" => Read(args, location),
            "WRITE" => Write(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown OSL.JSON method '{methodName}'."),
        };
    }

    private static OslangValue Parse(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "JSON.PARSE() expects exactly 1 argument (JSON text).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "JSON.PARSE() expects a STRING argument.");
        }

        try
        {
            using var doc = JsonDocument.Parse(sv.Value);
            return ParseJsonElement(doc.RootElement, location);
        }
        catch (JsonException ex)
        {
            throw new OslangRuntimeException(location, $"JSON.PARSE() failed: {ex.Message}");
        }
    }

    private static OslangValue ParseJsonElement(JsonElement element, SourceLocation location)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = new Dictionary<string, OslangValue>();
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = ParseJsonElement(prop.Value, location);
                }
                return new JsonObjectValue(obj);
            }
            case JsonValueKind.Array:
            {
                var arr = new List<OslangValue>();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(ParseJsonElement(item, location));
                }
                return new JsonArrayValue(arr);
            }
            case JsonValueKind.String:
                return new StringValue(element.GetString() ?? "");
            case JsonValueKind.Number:
                return new NumberValue(element.GetDouble());
            case JsonValueKind.True:
                return BooleanValue.True;
            case JsonValueKind.False:
                return BooleanValue.False;
            case JsonValueKind.Null:
                return OslangValue.Null;
            default:
                throw new OslangRuntimeException(location, $"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }

    private static OslangValue Stringify(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, "JSON.STRINGIFY() expects 1 or 2 arguments (value, pretty?).");
        }
        var pretty = args.Count >= 2 && args[1] is BooleanValue bv && bv.Value;
        try
        {
            var json = SerializeToJson(args[0], pretty ? 0 : -1, pretty ? "  " : "");
            return new StringValue(json);
        }
        catch (Exception ex)
        {
            throw new OslangRuntimeException(location, $"JSON.STRINGIFY() failed: {ex.Message}");
        }
    }

    private static OslangValue Pretty(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "JSON.PRETTY() expects exactly 1 argument (value).");
        }
        try
        {
            var json = SerializeToJson(args[0], 0, "  ");
            return new StringValue(json);
        }
        catch (Exception ex)
        {
            throw new OslangRuntimeException(location, $"JSON.PRETTY() failed: {ex.Message}");
        }
    }

    private static string SerializeToJson(OslangValue value, int depth, string indent)
    {
        var sb = new StringBuilder();
        SerializeValue(value, sb, depth, indent);
        return sb.ToString();
    }

    private static void SerializeValue(OslangValue value, StringBuilder sb, int depth, string indent)
    {
        switch (value)
        {
            case NullValue:
                sb.Append("null");
                break;
            case BooleanValue b:
                sb.Append(b.Value ? "true" : "false");
                break;
            case NumberValue n:
                sb.Append(n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case StringValue s:
                sb.Append(JsonSerializer.Serialize(s.Value));
                break;
            case JsonObjectValue obj:
                SerializeJsonObject(obj, sb, depth, indent);
                break;
            case JsonArrayValue arr:
                SerializeJsonArray(arr, sb, depth, indent);
                break;
            case ArrayValue arrVal:
            {
                var items = new List<OslangValue>(arrVal.Items);
                var ja = new JsonArrayValue(items);
                SerializeJsonArray(ja, sb, depth, indent);
                break;
            }
            default:
                throw new ArgumentException($"Cannot serialize type {value.TypeName} to JSON.");
        }
    }

    private static void SerializeJsonObject(JsonObjectValue obj, StringBuilder sb, int depth, string indent)
    {
        sb.Append('{');
        var entries = obj.Data.ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (depth >= 0 && !string.IsNullOrEmpty(indent))
            {
                sb.AppendLine();
                sb.Append(new string(' ', (depth + 1) * indent.Length));
            }
            sb.Append(JsonSerializer.Serialize(entries[i].Key));
            sb.Append(':');
            if (depth >= 0 && !string.IsNullOrEmpty(indent))
            {
                sb.Append(' ');
            }
            SerializeValue(entries[i].Value, sb, depth >= 0 ? depth + 1 : -1, indent);
        }
        if (entries.Count > 0 && depth >= 0 && !string.IsNullOrEmpty(indent))
        {
            sb.AppendLine();
            sb.Append(new string(' ', depth * indent.Length));
        }
        sb.Append('}');
    }

    private static void SerializeJsonArray(JsonArrayValue arr, StringBuilder sb, int depth, string indent)
    {
        sb.Append('[');
        for (var i = 0; i < arr.Items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (depth >= 0 && !string.IsNullOrEmpty(indent))
            {
                sb.AppendLine();
                sb.Append(new string(' ', (depth + 1) * indent.Length));
            }
            SerializeValue(arr.Items[i], sb, depth >= 0 ? depth + 1 : -1, indent);
        }
        if (arr.Items.Count > 0 && depth >= 0 && !string.IsNullOrEmpty(indent))
        {
            sb.AppendLine();
            sb.Append(new string(' ', depth * indent.Length));
        }
        sb.Append(']');
    }

    private static OslangValue Read(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "JSON.READ() expects exactly 1 argument (path).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "JSON.READ() expects a STRING argument for path.");
        }

        if (!File.Exists(sv.Value))
        {
            throw new OslangRuntimeException(location, $"JSON.READ() file not found: '{sv.Value}'.");
        }

        var text = File.ReadAllText(sv.Value);
        return Parse([new StringValue(text)], location);
    }

    private static OslangValue Write(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "JSON.WRITE() expects exactly 2 arguments (path, value).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "JSON.WRITE() expects a STRING argument for path.");
        }

        try
        {
            var json = SerializeToJson(args[1], -1, "");
            File.WriteAllText(sv.Value, json, Encoding.UTF8);
            return OslangValue.Null;
        }
        catch (Exception ex)
        {
            throw new OslangRuntimeException(location, $"JSON.WRITE() failed: {ex.Message}");
        }
    }
}
