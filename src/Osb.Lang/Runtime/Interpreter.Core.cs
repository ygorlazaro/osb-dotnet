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
    private readonly Dictionary<string, FunctionOverloadSet> _functions = new();
    private readonly Dictionary<string, Variable> _globals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClassDefinition> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InterfaceDefinition> _interfaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OslangValue> _standardLibraries = new();
    private readonly Dictionary<string, List<(string MemberName, OslangValue Value)>> _enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnumTypeValue> _enumTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UsingDecl> _topLevelUsings;
    private readonly List<EnumDecl> _topLevelEnums;
    private readonly ExtensionRegistry _extensions;
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly Action? _clear;
    private ObjectInstance? _currentObject;
    private ClassDefinition? _enclosingClass;
    private bool _inConstructor;

    public Interpreter(Osb.Lang.Compilation.Compilation compilation, ExtensionRegistry extensions, TextWriter output, TextReader input, Action? clear, IReadOnlyList<Ast.UsingDecl>? topLevelUsings = null, IReadOnlyList<Ast.EnumDecl>? topLevelEnums = null)
    {
        _extensions = extensions;
        _output = output;
        _input = input;
        _clear = clear;
        _topLevelUsings = topLevelUsings is null ? [] : new List<UsingDecl>(topLevelUsings);
        _topLevelEnums = topLevelEnums is null ? [] : new List<EnumDecl>(topLevelEnums);

        foreach (var kvp in compilation.Symbols.Functions)
        {
            _functions[kvp.Key] = kvp.Value;
        }

        foreach (var cls in compilation.Symbols.Classes)
        {
            _classes[cls.Key] = cls.Value;
        }

        foreach (var iface in compilation.Symbols.Interfaces)
        {
            _interfaces[iface.Key] = iface.Value;
        }
    }


    private FunctionOverloadSet? GetFunctionSet(string name)
    {
        return _functions.TryGetValue(name, out var set) ? set : null;
    }


    private FunctionDecl? GetFunction(string name)
    {
        var set = GetFunctionSet(name);
        return set?.Overloads.FirstOrDefault();
    }


    public void RegisterClass(ClassDefinition classDef)
    {
        if (!_classes.TryAdd(classDef.Name, classDef))
        {
            throw new SemanticException(SourceLocation.Unknown, $"Class '{classDef.Name}' is already registered.");
        }
    }


    public void RegisterInterface(InterfaceDefinition interfaceDef)
    {
        if (!_interfaces.TryAdd(interfaceDef.Name, interfaceDef))
        {
            throw new SemanticException(SourceLocation.Unknown, $"Interface '{interfaceDef.Name}' is already registered.");
        }
    }


    public ClassDefinition? GetClass(string name) => _classes.TryGetValue(name, out var cls) ? cls : null;


    public InterfaceDefinition? GetInterface(string name) => _interfaces.TryGetValue(name, out var iface) ? iface : null;


    public IReadOnlyDictionary<string, ClassDefinition> GetClasses() => _classes;


    public IReadOnlyDictionary<string, InterfaceDefinition> GetInterfaces() => _interfaces;


    public OslangValue Run(IReadOnlyList<OslangValue>? args = null)
    {
        var globalScope = new Scope(_globals);
        foreach (var usingDecl in _topLevelUsings)
        {
            ExecuteUsing(usingDecl, globalScope);
        }

        foreach (var enumDecl in _topLevelEnums)
        {
            ExecuteEnumDecl(enumDecl);
        }

        RegisterBuiltinKeyEnum();

        var main = GetFunction("MAIN") ?? throw new SemanticException(SourceLocation.Unknown, "Program has no FUNCTION MAIN().");

        var argsArray = args != null
            ? new ArrayValue(args.ToList(), RuntimeType.String)
            : new ArrayValue([], RuntimeType.String);
        _globals["ARGS"] = new Variable { Value = argsArray };

        if (main.Parameters.Count == 0)
        {
            return CallFunction("MAIN", [], SourceLocation.Unknown);
        }

        if (main.Parameters.Count == 1)
        {
            return CallFunction("MAIN", [argsArray], SourceLocation.Unknown);
        }

        throw new SemanticException(main.Location, "FUNCTION MAIN() must declare either no parameters or a single parameter (Args).");
    }

    // ============================================================
    // Chamadas de função
    // ============================================================
}
