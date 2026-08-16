using System.IO;
using System.Text;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 OSL.CSV standard library implementation.
/// </summary>
public static class OslCsvNamespace
{
    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        return upper switch
        {
            "PARSE" => Parse(args, location),
            "STRINGIFY" => Stringify(args, location),
            "READ" => Read(args, location),
            "WRITE" => Write(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown OSL.CSV method '{methodName}'."),
        };
    }

    private static OslangValue Parse(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, "CSV.PARSE() expects 1 or 2 arguments (text, hasHeader?).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CSV.PARSE() expects a STRING argument for text.");
        }

        var hasHeader = args.Count >= 2 && args[1] is BooleanValue b && b.Value;
        var rows = ParseCsv(sv.Value, hasHeader, location);

        var items = new List<OslangValue>();
        foreach (var row in rows)
        {
            var data = row.ToDictionary(kv => kv.Key, kv => (OslangValue)new StringValue(kv.Value), StringComparer.OrdinalIgnoreCase);
            items.Add(new JsonObjectValue(data));
        }
        return new ArrayValue(items, RuntimeType.Object);
    }

    private static List<Dictionary<string, string>> ParseCsv(string text, bool hasHeader, SourceLocation location)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0)
        {
            return [];
        }

        var startIndex = 0;
        List<string>? headers = null;

        if (hasHeader)
        {
            headers = ParseCsvRow(lines[0], location);
            startIndex = 1;
        }

        var rows = new List<Dictionary<string, string>>();
        for (var i = startIndex; i < lines.Count; i++)
        {
            var fields = ParseCsvRow(lines[i], location);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                for (var j = 0; j < headers.Count && j < fields.Count; j++)
                {
                    dict[headers[j]] = fields[j];
                }
            }
            else
            {
                for (var j = 0; j < fields.Count; j++)
                {
                    dict[j.ToString()] = fields[j];
                }
            }
            rows.Add(dict);
        }

        return rows;
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
            }
            else if (ch == '\n' && !inQuotes)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            else if (ch == '\r' && !inQuotes)
            {
                // skip
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private static List<string> ParseCsvRow(string line, SourceLocation location)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static OslangValue Stringify(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "CSV.STRINGIFY() expects exactly 1 argument (array).");
        }
        if (args[0] is not ArrayValue av)
        {
            throw new OslangRuntimeException(location, "CSV.STRINGIFY() expects an ARRAY argument.");
        }

        if (av.Items.Count == 0)
        {
            return new StringValue("");
        }

        List<string> headers;
        if (av.Items[0] is JsonObjectValue firstObj)
        {
            headers = firstObj.Data.Keys.ToList();
        }
        else
        {
            throw new OslangRuntimeException(location, "CSV.STRINGIFY() expects an array of JSON objects.");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

        foreach (var item in av.Items)
        {
            if (item is not JsonObjectValue obj)
            {
                throw new OslangRuntimeException(location, "CSV.STRINGIFY() expects an array of JSON objects.");
            }

            var row = new List<string>();
            foreach (var key in headers)
            {
                if (obj.Data.TryGetValue(key, out var val))
                {
                    row.Add(EscapeCsvField(Conversions.ToDisplayString(val, location)));
                }
                else
                {
                    row.Add("");
                }
            }
            sb.AppendLine(string.Join(",", row));
        }

        return new StringValue(sb.ToString());
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static OslangValue Read(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "CSV.READ() expects exactly 1 argument (path).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CSV.READ() expects a STRING argument for path.");
        }

        if (!File.Exists(sv.Value))
        {
            throw new OslangRuntimeException(location, $"CSV.READ() file not found: '{sv.Value}'.");
        }

        var text = File.ReadAllText(sv.Value);
        return Parse([new StringValue(text)], location);
    }

    private static OslangValue Write(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "CSV.WRITE() expects exactly 2 arguments (path, array).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CSV.WRITE() expects a STRING argument for path.");
        }

        try
        {
            var csvResult = Stringify([args[1]], location);
            if (csvResult is not StringValue csvStr)
            {
                throw new OslangRuntimeException(location, "CSV.WRITE() failed to produce string.");
            }
            File.WriteAllText(sv.Value, csvStr.Value, Encoding.UTF8);
            return OslangValue.Null;
        }
        catch (Exception ex)
        {
            throw new OslangRuntimeException(location, $"CSV.WRITE() failed: {ex.Message}");
        }
    }
}
