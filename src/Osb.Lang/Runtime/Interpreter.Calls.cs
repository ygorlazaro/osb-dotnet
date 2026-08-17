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
    private OslangValue CallFunction(string name, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        var set = GetFunctionSet(name);
        if (set is not null)
        {
            var resolved = set.Resolve(args.Select(a => a.Type).ToList());
            if (resolved is not null)
            {
                return CallUserFunction(resolved, args, callLocation);
            }
        }

        if (StandardLibrary.FunctionNames.Contains(name))
        {
            return StandardLibrary.Call(name, args, callLocation);
        }

        if (_currentObject is not null)
        {
            var method = _currentObject.ClassDefinition.FindMethod(name);
            if (method is not null)
            {
                CheckMemberVisibility(method.Visibility, method.Name, callLocation);
                return CallMethod(_currentObject, method, null, args, callLocation);
            }
        }

        if (_extensions.TryGet(name, out var hostFunction))
        {
            return hostFunction(args, callLocation);
        }

        throw new OslangRuntimeException(callLocation, $"Unknown function '{name}'.");
    }


    private OslangValue CallNamespaceMethod(string namespaceName, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (namespaceName.Equals("MATH", StringComparison.OrdinalIgnoreCase))
        {
            return MathNamespace.Call(methodName, args, location);
        }

        if (namespaceName.Equals("FILE", StringComparison.OrdinalIgnoreCase))
        {
            if (_extensions.TryGet($"FILE.{methodName}", out var fileFunc))
            {
                return fileFunc(args, location);
            }
            throw new OslangRuntimeException(location, $"Unknown FILE method '{methodName}'.");
        }

        if (namespaceName.Equals("DIR", StringComparison.OrdinalIgnoreCase))
        {
            if (_extensions.TryGet($"DIR.{methodName}", out var dirFunc))
            {
                return dirFunc(args, location);
            }
            throw new OslangRuntimeException(location, $"Unknown DIR method '{methodName}'.");
        }

        if (namespaceName.Equals("OSL", StringComparison.OrdinalIgnoreCase))
        {
            if (methodName.Equals("I18N", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.I18N");
            }

            if (methodName.Equals("JSON", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.JSON");
            }

            if (methodName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.CSV");
            }

            if (methodName.Equals("XML", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.XML");
            }

            if (methodName.Equals("CNF", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.CNF");
            }

            if (methodName.Equals("CONSOLE", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.CONSOLE");
            }

            if (methodName.Equals("APP", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.APP");
            }

            if (methodName.Equals("FILE", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSL.FILE");
            }

            throw new OslangRuntimeException(location, $"Unknown OSL module '{methodName}'. Available: I18N, JSON, CSV, XML, CNF, CONSOLE, APP, FILE.");
        }

        if (namespaceName.Equals("OSB", StringComparison.OrdinalIgnoreCase))
        {
            if (methodName.Equals("NET", StringComparison.OrdinalIgnoreCase))
            {
                return new ModuleValue("OSB.NET");
            }

            throw new OslangRuntimeException(location, $"Unknown OSB module '{methodName}'. Available: NET.");
        }

        throw new OslangRuntimeException(location, $"Unknown namespace '{namespaceName}'.");
    }


    private OslangValue DispatchConsole(ModuleValue module, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        if (args.Count == 0)
        {
            switch (upper)
            {
                case "BLACK": return new NumberValue(0);
                case "BLUE": return new NumberValue(1);
                case "GREEN": return new NumberValue(2);
                case "CYAN": return new NumberValue(3);
                case "RED": return new NumberValue(4);
                case "MAGENTA": return new NumberValue(5);
                case "YELLOW": return new NumberValue(6);
                case "WHITE": return new NumberValue(7);
                case "BRIGHT_BLACK": return new NumberValue(8);
                case "BRIGHT_BLUE": return new NumberValue(9);
                case "BRIGHT_GREEN": return new NumberValue(10);
                case "BRIGHT_CYAN": return new NumberValue(11);
                case "BRIGHT_RED": return new NumberValue(12);
                case "BRIGHT_MAGENTA": return new NumberValue(13);
                case "BRIGHT_YELLOW": return new NumberValue(14);
                case "BRIGHT_WHITE": return new NumberValue(15);
                case "ENTER": return new EnumValue(new NumberValue(1), "KEYCODE", "ENTER");
                case "ESC": return new EnumValue(new NumberValue(2), "KEYCODE", "ESC");
                case "TAB": return new EnumValue(new NumberValue(3), "KEYCODE", "TAB");
                case "BACKSPACE": return new EnumValue(new NumberValue(4), "KEYCODE", "BACKSPACE");
                case "DELETE": return new EnumValue(new NumberValue(5), "KEYCODE", "DELETE");
                case "INSERT": return new EnumValue(new NumberValue(6), "KEYCODE", "INSERT");
                case "SPACE": return new EnumValue(new NumberValue(7), "KEYCODE", "SPACE");
                case "UP": return new EnumValue(new NumberValue(8), "KEYCODE", "UP");
                case "DOWN": return new EnumValue(new NumberValue(9), "KEYCODE", "DOWN");
                case "LEFT": return new EnumValue(new NumberValue(10), "KEYCODE", "LEFT");
                case "RIGHT": return new EnumValue(new NumberValue(11), "KEYCODE", "RIGHT");
                case "HOME": return new EnumValue(new NumberValue(12), "KEYCODE", "HOME");
                case "END": return new EnumValue(new NumberValue(13), "KEYCODE", "END");
                case "PAGEUP": return new EnumValue(new NumberValue(14), "KEYCODE", "PAGEUP");
                case "PAGEDOWN": return new EnumValue(new NumberValue(15), "KEYCODE", "PAGEDOWN");
                case "F1": return new EnumValue(new NumberValue(16), "KEYCODE", "F1");
                case "F2": return new EnumValue(new NumberValue(17), "KEYCODE", "F2");
                case "F3": return new EnumValue(new NumberValue(18), "KEYCODE", "F3");
                case "F4": return new EnumValue(new NumberValue(19), "KEYCODE", "F4");
                case "F5": return new EnumValue(new NumberValue(20), "KEYCODE", "F5");
                case "F6": return new EnumValue(new NumberValue(21), "KEYCODE", "F6");
                case "F7": return new EnumValue(new NumberValue(22), "KEYCODE", "F7");
                case "F8": return new EnumValue(new NumberValue(23), "KEYCODE", "F8");
                case "F9": return new EnumValue(new NumberValue(24), "KEYCODE", "F9");
                case "F10": return new EnumValue(new NumberValue(25), "KEYCODE", "F10");
                case "F11": return new EnumValue(new NumberValue(26), "KEYCODE", "F11");
                case "F12": return new EnumValue(new NumberValue(27), "KEYCODE", "F12");
            }
        }

        if (_extensions.TryGet($"CONSOLE.{upper}", out var consoleFunc))
        {
            return consoleFunc(args, location);
        }

        throw new OslangRuntimeException(location, $"Unknown OSL.CONSOLE method '{methodName}'.");
    }


    private OslangValue DispatchApp(ModuleValue module, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        if (_extensions.TryGet($"APP.{upper}", out var appFunc))
        {
            return appFunc(args, location);
        }

        throw new OslangRuntimeException(location, $"Unknown OSL.APP method '{methodName}'.");
    }


    private OslangValue CallModuleMethod(ModuleValue module, string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var name = module.ModuleName.ToUpperInvariant();
        switch (name)
        {
            case "OSL.I18N":
                return I18nNamespace.Call(methodName, args, location);
            case "OSL.JSON":
                return OslJsonNamespace.Call(methodName, args, location);
            case "OSL.CSV":
                return OslCsvNamespace.Call(methodName, args, location);
            case "OSL.XML":
                return OslXmlNamespace.Call(methodName, args, location);
            case "OSL.CNF":
                return OslCnfNamespace.Call(methodName, args, location);
            case "OSL.CONSOLE":
                return DispatchConsole(module, methodName, args, location);
            case "OSL.APP":
                return DispatchApp(module, methodName, args, location);
            case "OSL.FILE":
                if (_extensions.TryGet($"FILE.{methodName}", out var fileFunc))
                {
                    return fileFunc(args, location);
                }
                throw new OslangRuntimeException(location, $"Unknown FILE method '{methodName}'.");
            case "OSB.NET":
                return OsbNetNamespace.Call(methodName, args, location);
            default:
                throw new OslangRuntimeException(location, $"Unknown module '{module.ModuleName}'.");
        }
    }


    private OslangValue CallUserFunction(FunctionDecl decl, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (args.Count != decl.Parameters.Count)
        {
            throw new OslangRuntimeException(
                callLocation,
                $"Function '{decl.Name}' expects {decl.Parameters.Count} argument(s), got {args.Count}.");
        }

        var scope = new Scope(_globals);
        for (var i = 0; i < decl.Parameters.Count; i++)
        {
            var param = decl.Parameters[i];
            var variable = scope.DeclareLocal(param.Name);
            if (param.TypeName is not null)
            {
                variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
            }

            TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
        }

        try
        {
            ExecuteBlock(decl.Body, scope, loopDepth: 0);
        }
        catch (ReturnSignal ret)
        {
            return ret.Value;
        }

        return OslangValue.Null; // seção 17: função sem RETURN explícito retorna NULL
    }

    // ============================================================
    // Statements
    // ============================================================
}
