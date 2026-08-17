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
    private static OslangValue DispatchCnfConfig(CnfConfigValue cnf, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "GET":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                if (cnf.Data.TryGetValue(sv.Value, out var val))
                {
                    return new StringValue(val);
                }
                return OslangValue.Null;
            case "SET":
                EnsureArgCount(args, 2, methodName, location);
                if (args[0] is not StringValue key || args[1] is not StringValue value)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects STRING arguments.");
                }
                cnf.Data[key.Value] = value.Value;
                return cnf;
            case "HAS":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv2)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                return BooleanValue.Of(cnf.Data.ContainsKey(sv2.Value));
            case "DELETE":
                EnsureArgCount(args, 1, methodName, location);
                if (args[0] is not StringValue sv3)
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument.");
                }
                cnf.Data.Remove(sv3.Value);
                return cnf;
            case "KEYS":
                EnsureArgCount(args, 0, methodName, location);
                return new JsonArrayValue(cnf.Data.Keys.Select(k => (OslangValue)new StringValue(k)).ToList());
            case "SAVE":
                if (args.Count == 0)
                {
                    if (string.IsNullOrEmpty(cnf.Path))
                    {
                        throw new OslangRuntimeException(location, "CNF.SAVE() requires a path or a previously loaded config.");
                    }
                    OslCnfNamespace.Save([cnf, new StringValue(cnf.Path)], location);
                    return OslangValue.Null;
                }
                if (args.Count == 1)
                {
                    if (args[0] is not StringValue path)
                    {
                        throw new OslangRuntimeException(location, $"{methodName}() expects a STRING argument for path.");
                    }
                    OslCnfNamespace.Save([cnf, path], location);
                    return OslangValue.Null;
                }
                throw new OslangRuntimeException(location, $"{methodName}() expects 0 or 1 arguments.");
            default:
                throw new OslangRuntimeException(location, $"Unknown method '{methodName}' on CNF config.");
        }
    }
}
