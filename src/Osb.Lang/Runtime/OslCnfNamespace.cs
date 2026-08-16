using System.IO;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 OSL.CNF standard library implementation.
/// </summary>
public static class OslCnfNamespace
{
    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        return upper switch
        {
            "READ" => Read(args, location),
            "WRITE" => Write(args, location),
            "GET" => Get(args, location),
            "SET" => Set(args, location),
            "HAS" => Has(args, location),
            "DELETE" => Delete(args, location),
            "KEYS" => Keys(args, location),
            "SAVE" => Save(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown OSL.CNF method '{methodName}'."),
        };
    }

    private static OslangValue Read(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "CNF.READ() expects exactly 1 argument (path).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.READ() expects a STRING argument for path.");
        }

        if (!File.Exists(sv.Value))
        {
            throw new OslangRuntimeException(location, $"CNF.READ() file not found: '{sv.Value}'.");
        }

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(sv.Value))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 1)
            {
                throw new OslangRuntimeException(location, $"Invalid CNF syntax in '{sv.Value}': '{rawLine}'.");
            }

            var key = line[..eq].Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw new OslangRuntimeException(location, $"Empty CNF key in '{sv.Value}'.");
            }

            var value = line[(eq + 1)..];
            data[key] = value;
        }

        return new CnfConfigValue(sv.Value, data);
    }

    private static OslangValue Write(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "CNF.WRITE() expects exactly 2 arguments (path, config).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.WRITE() expects a STRING argument for path.");
        }
        if (args[1] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.WRITE() expects a CNFCONFIG argument.");
        }

        var lines = config.Data.Select(kv => $"{kv.Key}={kv.Value}");
        File.WriteAllLines(sv.Value, lines);
        return OslangValue.Null;
    }

    private static OslangValue Get(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "CNF.GET() expects exactly 2 arguments (config, key).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.GET() expects a CNFCONFIG argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.GET() expects a STRING argument for key.");
        }

        if (config.Data.TryGetValue(sv.Value, out var val))
        {
            return new StringValue(val);
        }

        return OslangValue.Null;
    }

    private static OslangValue Set(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 3)
        {
            throw new OslangRuntimeException(location, "CNF.SET() expects exactly 3 arguments (config, key, value).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.SET() expects a CNFCONFIG argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.SET() expects a STRING argument for key.");
        }
        if (args[2] is not StringValue val)
        {
            throw new OslangRuntimeException(location, "CNF.SET() expects a STRING argument for value.");
        }

        var newData = new Dictionary<string, string>(config.Data, StringComparer.OrdinalIgnoreCase)
        {
            [sv.Value] = val.Value
        };
        return new CnfConfigValue(config.Path, newData);
    }

    private static OslangValue Has(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "CNF.HAS() expects exactly 2 arguments (config, key).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.HAS() expects a CNFCONFIG argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.HAS() expects a STRING argument for key.");
        }

        return BooleanValue.Of(config.Data.ContainsKey(sv.Value));
    }

    private static OslangValue Delete(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "CNF.DELETE() expects exactly 2 arguments (config, key).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.DELETE() expects a CNFCONFIG argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "CNF.DELETE() expects a STRING argument for key.");
        }

        var newData = new Dictionary<string, string>(config.Data, StringComparer.OrdinalIgnoreCase);
        newData.Remove(sv.Value);
        return new CnfConfigValue(config.Path, newData);
    }

    private static OslangValue Keys(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "CNF.KEYS() expects exactly 1 argument (config).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.KEYS() expects a CNFCONFIG argument.");
        }

        var items = config.Data.Keys.Select(k => (OslangValue)new StringValue(k)).ToList();
        return new JsonArrayValue(items);
    }

    public static OslangValue Save(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            throw new OslangRuntimeException(location, "CNF.SAVE() expects 1 or 2 arguments (config, path?).");
        }
        if (args[0] is not CnfConfigValue config)
        {
            throw new OslangRuntimeException(location, "CNF.SAVE() expects a CNFCONFIG argument.");
        }

        var path = args.Count >= 2
            ? (args[1] is StringValue sv ? sv.Value : throw new OslangRuntimeException(location, "CNF.SAVE() expects a STRING argument for path."))
            : config.Path;

        if (string.IsNullOrEmpty(path))
        {
            throw new OslangRuntimeException(location, "CNF.SAVE() has no path to save to. Provide a path argument.");
        }

        var lines = config.Data.Select(kv => $"{kv.Key}={kv.Value}");
        File.WriteAllLines(path, lines);
        return OslangValue.Null;
    }
}
