using Osb.Lang.Ast;
using Osb.Lang.Compilation;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

/// <summary>
/// Interpretador tree-walking de OSLANG 0.1/0.2.
/// </summary>
internal sealed class Interpreter
{
    private readonly Dictionary<string, FunctionOverloadSet> _functions = new();
    private readonly Dictionary<string, Variable> _globals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClassDefinition> _classes = new();
    private readonly Dictionary<string, InterfaceDefinition> _interfaces = new();
    private readonly Dictionary<string, OslangValue> _standardLibraries = new();
    private readonly Dictionary<string, List<(string MemberName, OslangValue Value)>> _enums = new(StringComparer.OrdinalIgnoreCase);
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

        var main = GetFunction("MAIN") ?? throw new SemanticException(SourceLocation.Unknown, "Program has no FUNCTION MAIN().");

        if (main.Parameters.Count == 0)
        {
            return CallFunction("MAIN", [], SourceLocation.Unknown);
        }

        if (main.Parameters.Count == 1)
        {
            var argsArray = args != null
                ? new ArrayValue(args.ToList(), RuntimeType.String)
                : new ArrayValue([], RuntimeType.String);
            return CallFunction("MAIN", [argsArray], SourceLocation.Unknown);
        }

        throw new SemanticException(main.Location, "FUNCTION MAIN() must declare either no parameters or a single parameter (Args).");
    }

    // ============================================================
    // Chamadas de função
    // ============================================================

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

            throw new OslangRuntimeException(location, $"Unknown OSL module '{methodName}'. Only OSL.I18N is available in 0.6.");
        }

        throw new OslangRuntimeException(location, $"Unknown namespace '{namespaceName}'.");
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

    private void ExecuteBlock(IReadOnlyList<Stmt> statements, Scope scope, int loopDepth, bool inSwitch = false)
    {
        foreach (var stmt in statements)
        {
            ExecuteStatement(stmt, scope, loopDepth, inSwitch);
        }
    }

    private void ExecuteStatement(Stmt stmt, Scope scope, int loopDepth, bool inSwitch = false)
    {
        switch (stmt)
        {
            case VarDeclStmt v:
                ExecuteVarDecl(v, scope);
                break;
            case GlobalDeclStmt g:
                ExecuteGlobalDecl(g, scope);
                break;
            case AssignStmt a:
                ExecuteAssign(a, scope);
                break;
            case ExpressionStmt e:
                Eval(e.Expression, scope);
                break;
            case PrintStmt p:
                ExecutePrint(p, scope);
                break;
            case ShowStmt s:
                ExecuteShow(s, scope);
                break;
            case InputStmt i:
                ExecuteInput(i, scope);
                break;
            case ClearStmt:
                _clear?.Invoke();
                break;
            case IfStmt f:
                ExecuteIf(f, scope, loopDepth);
                break;
            case ForStmt f:
                ExecuteFor(f, scope, loopDepth);
                break;
            case WhileStmt w:
                ExecuteWhile(w, scope, loopDepth);
                break;
            case DoWhileStmt d:
                ExecuteDoWhile(d, scope, loopDepth);
                break;
            case BreakStmt b:
                if (loopDepth == 0 && !inSwitch)
                {
                    throw new OslangRuntimeException(b.Location, "BREAK used outside of a loop or SWITCH.");
                }

                throw new BreakSignal();
            case ContinueStmt c:
                if (loopDepth == 0)
                {
                    throw new OslangRuntimeException(c.Location, "CONTINUE used outside of a loop.");
                }

                throw new ContinueSignal();
            case ReturnStmt r:
                throw new ReturnSignal(r.Value is not null ? Eval(r.Value, scope) : OslangValue.Null);
            case TryCatchStmt t:
                ExecuteTryCatch(t, scope, loopDepth);
                break;
            case BaseCallStmt b:
                ExecuteBaseCall(b, scope);
                break;
            case SwitchStmt s:
                ExecuteSwitch(s, scope, loopDepth);
                break;
            case UsingDecl u:
                ExecuteUsing(u, scope);
                break;
            case EnumDecl e:
                ExecuteEnumDecl(e);
                break;
            default:
                throw new InvalidOperationException($"Unknown statement node {stmt.GetType().Name}.");
        }
    }

    private void ExecuteBaseCall(BaseCallStmt baseCall, Scope scope)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(baseCall.Location, "BASE can only be used inside a class method.");
        }

        var classDef = _enclosingClass ?? _currentObject.ClassDefinition;
        if (classDef.BaseClass is null)
        {
            throw new OslangRuntimeException(baseCall.Location, "BASE can only be used in a derived class.");
        }

        var baseClass = classDef.BaseClass;
        if (baseClass.Constructor is null)
        {
            throw new OslangRuntimeException(baseCall.Location, $"Base class '{baseClass.Name}' has no constructor.");
        }

        var args = baseCall.Args.Select(a => Eval(a, scope)).ToList();
        ExecuteConstructor(_currentObject, baseClass.Constructor, args, baseClass, baseCall.Location);
    }

    private void ExecuteVarDecl(VarDeclStmt v, Scope scope)
    {
        var variable = scope.DeclareLocal(v.Name);
        if (v.TypeName is not null)
        {
            var type = TypeSystem.ParseTypeName(v.TypeName);
            variable.EstablishedType = type;
            variable.Value = TypeSystem.DefaultValueFor(type); // seção 12
        }
    }

    private void ExecuteGlobalDecl(GlobalDeclStmt g, Scope scope)
    {
        var value = Eval(g.Value, scope);
        var variable = scope.DeclareOrGetGlobal(g.Name);
        TypeSystem.Assign(variable, value, g.Location, $"global variable '{g.Name}'");
    }

    private void ExecuteAssign(AssignStmt a, Scope scope)
    {
        var value = Eval(a.Value, scope);
        switch (a.Target)
        {
            case VariableTarget vt:
                var variable = scope.ResolveForAssignment(vt.Name);
                TypeSystem.Assign(variable, value, vt.Location, $"variable '{vt.Name}'");
                break;
            case IndexTarget it:
                var array = EvalArray(it.ArrayExpr, scope);
                var index = ResolveIndex(Eval(it.IndexExpr, scope), array, it.Location);
                TypeSystem.AssignArrayElement(array, index, value, it.Location);
                break;
            case MemberTarget mt:
                var memberValue = Eval(mt.Object, scope);
                if (memberValue is not ObjectValue objectValue)
                {
                    throw new OslangRuntimeException(mt.Location, $"Cannot assign to member on type {memberValue.TypeName}.");
                }
                AssignToMember(objectValue.Instance, mt.MemberName, value, mt.Location);
                break;
            default:
                throw new InvalidOperationException($"Unknown assign target {a.Target.GetType().Name}.");
        }
    }

    private void AssignToMember(ObjectInstance instance, string memberName, OslangValue value, SourceLocation location)
    {
        var classDef = instance.ClassDefinition;
        var prop = classDef.FindProperty(memberName);
        if (prop is null)
        {
            throw new OslangRuntimeException(location, $"Property '{memberName}' not found in class '{classDef.Name}'.");
        }

        if (instance.PropertyValues.TryGetValue(prop.Name, out var existingValue))
        {
            if (existingValue.Type != RuntimeType.Null)
            {
                if (existingValue.Type != value.Type && value.Type != RuntimeType.Null)
                {
                    throw new OslangRuntimeException(location, $"Type error: cannot assign {value.TypeName} to property '{prop.Name}', which is {existingValue.TypeName}.");
                }
            }
            else if (value.Type != RuntimeType.Null)
            {
                if (prop.TypeName is not null)
                {
                    var expectedType = TypeSystem.ParseTypeName(prop.TypeName);
                    if (expectedType != value.Type)
                    {
                        throw new OslangRuntimeException(location, $"Type error: property '{prop.Name}' expects {expectedType}, got {value.TypeName}.");
                    }
                }
            }
        }

        instance.PropertyValues[prop.Name] = value;
    }

    private void ExecuteUsing(UsingDecl usingDecl, Scope scope)
    {
        var moduleName = usingDecl.ModuleName;

        if (!moduleName.StartsWith("OSL.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var shortName = moduleName.Split('.')[^1];
        if (_standardLibraries.ContainsKey(shortName))
        {
            return;
        }

        if (moduleName.Equals("OSL.I18N", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.I18N");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        throw new OslangRuntimeException(usingDecl.Location, $"Unknown standard library module '{moduleName}'.");
    }

    private void ExecutePrint(PrintStmt p, Scope scope)
    {
        var text = string.Concat(p.Expressions.Select(e => Conversions.ToDisplayString(Eval(e, scope), e.Location)));
        _output.WriteLine(text);
    }

    private void ExecuteShow(ShowStmt s, Scope scope)
    {
        var text = string.Concat(s.Expressions.Select(e => Conversions.ToDisplayString(Eval(e, scope), e.Location)));
        _output.Write(text);
    }

    private void ExecuteInput(InputStmt i, Scope scope)
    {
        var line = _input.ReadLine() ?? string.Empty;
        var variable = scope.ResolveForAssignment(i.VariableName);
        TypeSystem.Assign(variable, new StringValue(line), i.Location, $"variable '{i.VariableName}'"); // seção 38: INPUT sempre retorna STRING
    }

    private void ExecuteIf(IfStmt f, Scope scope, int loopDepth)
    {
        if (Conversions.IsTruthy(Eval(f.Condition, scope)))
        {
            ExecuteBlock(f.ThenBody, scope, loopDepth);
            return;
        }

        foreach (var elif in f.ElifBranches)
        {
            if (Conversions.IsTruthy(Eval(elif.Condition, scope)))
            {
                ExecuteBlock(elif.Body, scope, loopDepth);
                return;
            }
        }

        if (f.ElseBody is not null)
        {
            ExecuteBlock(f.ElseBody, scope, loopDepth);
        }
    }

    private void ExecuteFor(ForStmt f, Scope scope, int loopDepth)
    {
        var start = EvalNumber(f.Start, scope, "FOR start value");
        var end = EvalNumber(f.End, scope, "FOR end value");
        var step = f.Step is not null ? EvalNumber(f.Step, scope, "STEP value") : 1;

        if (step == 0)
        {
            throw new OslangRuntimeException(f.Location, "STEP cannot be zero.");
        }

        var variable = scope.ResolveForAssignment(f.VariableName);
        var current = start;

        while (step > 0 ? current <= end : current >= end)
        {
            TypeSystem.Assign(variable, new NumberValue(current), f.Location, $"loop variable '{f.VariableName}'");

            try
            {
                ExecuteBlock(f.Body, scope, loopDepth + 1);
            }
            catch (BreakSignal)
            {
                break;
            }
            catch (ContinueSignal)
            {
                // segue para o incremento abaixo
            }

            current += step;
        }
    }

    private void ExecuteWhile(WhileStmt w, Scope scope, int loopDepth)
    {
        while (Conversions.IsTruthy(Eval(w.Condition, scope)))
        {
            try
            {
                ExecuteBlock(w.Body, scope, loopDepth + 1);
            }
            catch (BreakSignal)
            {
                break;
            }
            catch (ContinueSignal)
            {
                // continua para a reavaliação da condição
            }
        }
    }

    private void ExecuteDoWhile(DoWhileStmt d, Scope scope, int loopDepth)
    {
        // Seção 32: a condição de DO WHILE é avaliada ANTES de cada iteração
        // (podendo executar zero vezes) - portanto, semanticamente idêntico a
        // WHILE, apesar do nome. Ver comentário em Ast/AstNodes.cs.
        while (Conversions.IsTruthy(Eval(d.Condition, scope)))
        {
            try
            {
                ExecuteBlock(d.Body, scope, loopDepth + 1);
            }
            catch (BreakSignal)
            {
                break;
            }
            catch (ContinueSignal)
            {
                // continua para a reavaliação da condição
            }
        }
    }

    private void ExecuteTryCatch(TryCatchStmt t, Scope scope, int loopDepth)
    {
        try
        {
            ExecuteBlock(t.TryBody, scope, loopDepth);
        }
        catch (OslangRuntimeException ex)
        {
            var errVariable = scope.DeclareLocal(t.CatchVariableName);
            errVariable.EstablishedType = RuntimeType.String;
            errVariable.Value = new StringValue(ex.Message);
            ExecuteBlock(t.CatchBody, scope, loopDepth);
        }
    }

    private void ExecuteSwitch(SwitchStmt s, Scope scope, int loopDepth)
    {
        var switchValue = Eval(s.Expression, scope);

        foreach (var caseClause in s.Cases)
        {
            var caseValue = Eval(caseClause.Value, scope);
            bool matched = (switchValue, caseValue) switch
            {
                (EnumValue sv, EnumSetValue ev) => ev.Values.Contains(sv),
                _ => ValuesEqual(switchValue, caseValue),
            };
            if (matched)
            {
                try
                {
                    ExecuteBlock(caseClause.Body, scope, loopDepth, inSwitch: true);
                }
                catch (BreakSignal)
                {
                    return;
                }

                return;
            }
        }

        if (s.DefaultCase is not null)
        {
            try
            {
                ExecuteBlock(s.DefaultCase.Body, scope, loopDepth, inSwitch: true);
            }
            catch (BreakSignal)
            {
                return;
            }
        }
    }

    private void ExecuteEnumDecl(EnumDecl e)
    {
        var members = new List<(string MemberName, OslangValue Value)>();
        var underlyingType = (RuntimeType?)null;

        foreach (var member in e.Members)
        {
            if (member.Value is null)
            {
                var index = members.Count;
                OslangValue value = underlyingType == RuntimeType.String
                    ? new StringValue(index.ToString())
                    : new NumberValue(index);
                members.Add((member.Name, value));
            }
            else
            {
                var evaluatedValue = Eval(member.Value, new Scope(_globals));
                if (underlyingType is null)
                {
                    underlyingType = evaluatedValue.Type;
                    if (underlyingType == RuntimeType.String)
                    {
                        for (var i = 0; i < members.Count; i++)
                        {
                            if (members[i].Value is NumberValue nv)
                            {
                                members[i] = (members[i].MemberName, new StringValue(nv.Value.ToString()));
                            }
                        }
                    }
                }
                else if (evaluatedValue.Type != underlyingType)
                {
                    throw new OslangRuntimeException(member.Location, $"Enum member '{member.Name}' has inconsistent type. Expected {underlyingType}, got {evaluatedValue.Type}.");
                }
                members.Add((member.Name, evaluatedValue));
            }
        }

        _enums[e.Name] = members;
        _globals[e.Name] = new Variable { Value = new EnumTypeValue(e.Name) };
    }

    // ============================================================
    // Expressões
    // ============================================================

    private OslangValue Eval(Expr expr, Scope scope) => expr switch
    {
        NumberLiteralExpr n => new NumberValue(n.Value),
        StringLiteralExpr s => EvalStringLiteral(s, scope),
        BooleanLiteralExpr b => BooleanValue.Of(b.Value),
        NullLiteralExpr => OslangValue.Null,
        ArrayLiteralExpr arr => EvalArrayLiteral(arr, scope),
        IdentifierExpr id => EvalIdentifier(id, scope),
        IndexExpr ix => EvalIndex(ix, scope),
        CallExpr call => EvalCall(call, scope),
        MethodCallExpr call => EvalMethodCall(call, scope),
        MemberAccessExpr ma => EvalMemberAccess(ma, scope),
        NewExpr ne => EvalNew(ne, scope),
        MeExpr => EvalMe(scope),
        BaseExpr => EvalBase(scope),
        UnaryExpr u => EvalUnary(u, scope),
        BinaryExpr b => EvalBinary(b, scope),
        SwitchExpr s => EvalSwitchExpr(s, scope),
        NamespaceExpr ns => EvalNamespace(ns, scope),
        ArrowFunctionExpr a => EvalArrowFunction(a, scope),
        BlockArrowFunctionExpr b => EvalBlockArrowFunction(b, scope),
        PostfixExpr p => EvalPostfix(p, scope),
        EnumSetExpr es => EvalEnumSet(es, scope),
        InterpolatedStringExpr ise => EvalInterpolatedString(ise, scope),
        _ => throw new InvalidOperationException($"Unknown expression node {expr.GetType().Name}."),
    };

    private OslangValue EvalStringLiteral(StringLiteralExpr s, Scope scope)
    {
        var value = s.Value;
        var parts = new List<InterpolatedStringPart>();
        var index = 0;

        while (true)
        {
            var start = value.IndexOf("${", index, StringComparison.Ordinal);
            if (start < 0)
            {
                parts.Add(new InterpolatedStringLiteral(value[index..], s.Location));
                break;
            }

            if (start > 0 && value[start - 1] == '\\')
            {
                parts.Add(new InterpolatedStringLiteral(value[index..(start - 1)], s.Location));
                parts.Add(new InterpolatedStringLiteral("${", s.Location));
                index = start + 2;
                continue;
            }

            parts.Add(new InterpolatedStringLiteral(value[index..start], s.Location));

            var end = value.IndexOf('}', start + 2);
            if (end < 0)
            {
                throw new OslangRuntimeException(s.Location, "Unterminated string interpolation.");
            }

            var exprText = value[(start + 2)..end];
            var expr = ParseExpressionFromString(exprText, s.Location);
            parts.Add(new InterpolatedStringExpression(expr, s.Location));
            index = end + 1;
        }

        if (parts.Count == 1 && parts[0] is InterpolatedStringLiteral literal)
        {
            return new StringValue(literal.Value);
        }

        return EvalInterpolatedString(new InterpolatedStringExpr(parts, s.Location), scope);
    }

    private static Expr ParseExpressionFromString(string exprText, SourceLocation location)
    {
        var tokens = new Lexer(exprText, location.Line, location.Column).Tokenize().ToList();
        var parser = new Parser(tokens);
        return parser.ParseExpression();
    }

    private double EvalNumber(Expr expr, Scope scope, string description)
    {
        var value = Eval(expr, scope);
        if (value is not NumberValue n)
        {
            throw new OslangRuntimeException(expr.Location, $"{description} must be a NUMBER, got {value.TypeName}.");
        }

        return n.Value;
    }

    private OslangValue EvalIdentifier(IdentifierExpr id, Scope scope)
    {
        var variable = scope.TryResolve(id.Name);
        if (variable is not null)
        {
            return variable.Value;
        }

        if (_currentObject is not null)
        {
            var prop = _currentObject.ClassDefinition.FindProperty(id.Name);
            if (prop is not null)
            {
                CheckMemberVisibility(prop.Visibility, prop.Name, id.Location);
                if (_currentObject.PropertyValues.TryGetValue(prop.Name, out var value))
                {
                    return value;
                }
                return OslangValue.Null;
            }

            var method = _currentObject.ClassDefinition.FindMethod(id.Name);
            if (method is not null)
            {
                CheckMemberVisibility(method.Visibility, method.Name, id.Location);
                return CreateMethodReference(method);
            }
        }

        if (_functions.TryGetValue(id.Name, out var funcSet))
        {
            var first = funcSet.Overloads.FirstOrDefault();
            if (first is not null)
            {
                return CreateFunctionReference(first);
            }
        }

        throw new OslangRuntimeException(id.Location, $"Undefined variable '{id.Name}'.");
    }

    private OslangValue EvalEnumSet(EnumSetExpr expr, Scope scope)
    {
        var left = Eval(expr.Left, scope);
        var right = Eval(expr.Right, scope);

        if (left is not EnumValue leftEnum && left is not EnumSetValue leftSet)
        {
            throw new OslangRuntimeException(expr.Location, "Enum set operator '|' requires enum values.");
        }

        if (right is not EnumValue rightEnum && right is not EnumSetValue rightSet)
        {
            throw new OslangRuntimeException(expr.Location, "Enum set operator '|' requires enum values.");
        }

        var leftType = left is EnumValue le ? le.EnumTypeName : ((EnumSetValue)left).EnumTypeName;
        var rightType = right is EnumValue re ? re.EnumTypeName : ((EnumSetValue)right).EnumTypeName;

        if (!string.Equals(leftType, rightType, StringComparison.OrdinalIgnoreCase))
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot combine enum values from different types: {leftType} and {rightType}.");
        }

        var result = new HashSet<EnumValue>();

        if (left is EnumSetValue ls)
        {
            result.UnionWith(ls.Values);
        }
        else if (left is EnumValue lv)
        {
            result.Add(lv);
        }

        if (right is EnumSetValue rs)
        {
            result.UnionWith(rs.Values);
        }
        else if (right is EnumValue rv)
        {
            result.Add(rv);
        }

        return new EnumSetValue(leftType, result);
    }

    private OslangValue EvalInterpolatedString(InterpolatedStringExpr expr, Scope scope)
    {
        var result = new System.Text.StringBuilder();
        foreach (var part in expr.Parts)
        {
            switch (part)
            {
                case InterpolatedStringLiteral literal:
                    result.Append(literal.Value);
                    break;
                case InterpolatedStringExpression expression:
                    var value = Eval(expression.Expression, scope);
                    result.Append(Conversions.ToDisplayString(value, expression.Location));
                    break;
            }
        }
        return new StringValue(result.ToString());
    }

    private OslangValue CreateFunctionReference(FunctionDecl decl)
    {
        return new FunctionValue((args, location) =>
        {
            if (args.Count != decl.Parameters.Count)
            {
                throw new OslangRuntimeException(location, $"Function '{decl.Name}' expects {decl.Parameters.Count} argument(s), got {args.Count}.");
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

            return OslangValue.Null;
        });
    }

    private OslangValue CreateMethodReference(MethodDefinition method)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, $"Cannot reference method '{method.Name}' outside of an object context.");
        }

        var instance = _currentObject;
        var classDef = instance.ClassDefinition;
        var declaringClass = FindMethodDeclaringClass(classDef, method.Name);

        return new FunctionValue((args, location) =>
        {
            if (args.Count != method.Parameters.Count)
            {
                throw new OslangRuntimeException(location, $"Method '{method.Name}' expects {method.Parameters.Count} argument(s), got {args.Count}.");
            }

            var previousObject = _currentObject;
            var previousEnclosing = _enclosingClass;
            _currentObject = instance;
            _enclosingClass = declaringClass ?? classDef;

            var scope = new Scope(_globals);
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var variable = scope.DeclareLocal(param.Name);
                if (param.TypeName is not null)
                {
                    variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
                }
                TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
            }

            try
            {
                ExecuteBlock(method.Body, scope, loopDepth: 0);
            }
            catch (ReturnSignal ret)
            {
                return ret.Value;
            }
            finally
            {
                _currentObject = previousObject;
                _enclosingClass = previousEnclosing;
            }

            return OslangValue.Null;
        });
    }

    private void CheckMemberVisibility(Visibility memberVisibility, string memberName, SourceLocation location)
    {
        if (memberVisibility == Visibility.Public)
        {
            return;
        }

        var accessingClass = _enclosingClass;
        if (accessingClass is null && _currentObject is not null)
        {
            accessingClass = _currentObject.ClassDefinition;
        }

        var declaringClass = FindDeclaringClass(memberName);

        if (memberVisibility == Visibility.Private)
        {
            if (declaringClass is null || !ReferenceEquals(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PRIVATE in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
        else if (memberVisibility == Visibility.Protected)
        {
            if (declaringClass is null || !IsDerivedFrom(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PROTECTED in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
    }

    private ClassDefinition? FindDeclaringClass(string memberName)
    {
        if (_currentObject is null)
        {
            return null;
        }

        var current = _currentObject.ClassDefinition;
        while (current != null)
        {
            if (current.Properties.Any(p => p.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            if (current.Methods.Any(m => m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            current = current.BaseClass;
        }

        return null;
    }

    private static bool IsDerivedFrom(ClassDefinition? derived, ClassDefinition? baseClass)
    {
        var current = derived;
        while (current != null)
        {
            if (ReferenceEquals(current, baseClass))
            {
                return true;
            }
            current = current.BaseClass;
        }

        return false;
    }

    private ArrayValue EvalArray(Expr expr, Scope scope)
    {
        var value = Eval(expr, scope);
        if (value is not ArrayValue array)
        {
            throw new OslangRuntimeException(expr.Location, $"Expected an ARRAY, got {value.TypeName}.");
        }

        return array;
    }

    private OslangValue EvalIndex(IndexExpr ix, Scope scope)
    {
        var array = EvalArray(ix.Array, scope);
        var index = ResolveIndex(Eval(ix.Index, scope), array, ix.Location);
        return array.Items[index];
    }

    private static int ResolveIndex(OslangValue indexValue, ArrayValue array, SourceLocation location)
    {
        if (indexValue is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"Array index must be a NUMBER, got {indexValue.TypeName}.");
        }

        if (n.Value != Math.Floor(n.Value))
        {
            throw new OslangRuntimeException(location, "Array index must be an integer.");
        }

        var index = (int)n.Value;
        if (index < 0 || index >= array.Items.Count)
        {
            throw new OslangRuntimeException(location, $"Array index {index} is out of range (array has {array.Items.Count} element(s)).");
        }

        return index;
    }

    private OslangValue EvalArrayLiteral(ArrayLiteralExpr expr, Scope scope)
    {
        var items = new List<OslangValue>(expr.Elements.Count);
        RuntimeType? elementType = null;

        foreach (var elementExpr in expr.Elements)
        {
            var value = Eval(elementExpr, scope);
            if (value.Type != RuntimeType.Null)
            {
                if (elementType is null)
                {
                    elementType = value.Type;
                }
                else if (elementType != value.Type)
                {
                    throw new OslangRuntimeException(
                        elementExpr.Location,
                        "Mixed-type arrays are not allowed - all elements of an ARRAY must be the same type.");
                }
            }

            items.Add(value);
        }

        return new ArrayValue(items, elementType);
    }

    private OslangValue EvalCall(CallExpr call, Scope scope)
    {
        var args = call.Args.Select(a => Eval(a, scope)).ToList();
        
        var variable = scope.TryResolve(call.Name);
        if (variable is not null && variable.Value is FunctionValue func)
        {
            return func.Callback(args, call.Location);
        }
        
        return CallFunction(call.Name, args, call.Location);
    }

    private OslangValue EvalUnary(UnaryExpr u, Scope scope)
    {
        if (u.Op == "NOT")
        {
            return BooleanValue.Of(!Conversions.IsTruthy(Eval(u.Operand, scope)));
        }

        if (u.Op == "++" || u.Op == "--")
        {
            throw new OslangRuntimeException(u.Location, $"Prefix '{u.Op}' is not allowed. Use postfix form (e.g. Counter{u.Op}).");
        }

        // "-" (menos unário)
        var operand = Eval(u.Operand, scope);
        if (operand is not NumberValue n)
        {
            throw new OslangRuntimeException(u.Location, $"Unary '-' requires a NUMBER operand, got {operand.TypeName}.");
        }

        return new NumberValue(-n.Value);
    }

    private OslangValue EvalBinary(BinaryExpr b, Scope scope)
    {
        // AND/OR usam curto-circuito (seção 25) - o operando direito só é avaliado quando necessário.
        if (b.Op == "AND")
        {
            var left = Conversions.IsTruthy(Eval(b.Left, scope));
            if (!left)
            {
                return BooleanValue.False;
            }

            return BooleanValue.Of(Conversions.IsTruthy(Eval(b.Right, scope)));
        }

        if (b.Op == "OR")
        {
            var left = Conversions.IsTruthy(Eval(b.Left, scope));
            if (left)
            {
                return BooleanValue.True;
            }

            return BooleanValue.Of(Conversions.IsTruthy(Eval(b.Right, scope)));
        }

        var leftValue = Eval(b.Left, scope);
        var rightValue = Eval(b.Right, scope);

        return b.Op switch
        {
            "+" => EvalPlus(leftValue, rightValue, b.Location),
            "-" => NumericOp(leftValue, rightValue, b.Location, "-", (x, y) => x - y),
            "*" => NumericOp(leftValue, rightValue, b.Location, "*", (x, y) => x * y),
            "/" => EvalDivide(leftValue, rightValue, b.Location),
            "%" => EvalModulo(leftValue, rightValue, b.Location),
            "MOD" => EvalModulo(leftValue, rightValue, b.Location),
            "**" => EvalPower(leftValue, rightValue, b.Location),
            "=" => BooleanValue.Of(ValuesEqual(leftValue, rightValue)),
            "<>" => BooleanValue.Of(!ValuesEqual(leftValue, rightValue)),
            "<" => CompareOp(leftValue, rightValue, b.Location, "<", (x, y) => x < y),
            ">" => CompareOp(leftValue, rightValue, b.Location, ">", (x, y) => x > y),
            "<=" => CompareOp(leftValue, rightValue, b.Location, "<=", (x, y) => x <= y),
            ">=" => CompareOp(leftValue, rightValue, b.Location, ">=", (x, y) => x >= y),
            _ => throw new InvalidOperationException($"Unknown binary operator '{b.Op}'."),
        };
    }

    private OslangValue EvalPlus(OslangValue left, OslangValue right, SourceLocation location)
    {
        // seção 23: + concatena quando qualquer um dos operandos é STRING.
        if (left.Type == RuntimeType.String || right.Type == RuntimeType.String)
        {
            return new StringValue(Conversions.ToDisplayString(left, location) + Conversions.ToDisplayString(right, location));
        }

        if (left is NumberValue l && right is NumberValue r)
        {
            return new NumberValue(l.Value + r.Value);
        }

        throw new OslangRuntimeException(location, $"Invalid operation '+' between {left.TypeName} and {right.TypeName}.");
    }

    private static OslangValue NumericOp(OslangValue left, OslangValue right, SourceLocation location, string op, Func<double, double, double> fn)
    {
        if (left is NumberValue l && right is NumberValue r)
        {
            return new NumberValue(fn(l.Value, r.Value));
        }

        throw new OslangRuntimeException(location, $"Invalid operation '{op}' between {left.TypeName} and {right.TypeName}.");
    }

    private static OslangValue EvalDivide(OslangValue left, OslangValue right, SourceLocation location)
    {
        if (left is not NumberValue l || right is not NumberValue r)
        {
            throw new OslangRuntimeException(location, $"Invalid operation '/' between {left.TypeName} and {right.TypeName}.");
        }

        if (r.Value == 0)
        {
            throw new OslangRuntimeException(location, "Division by zero.");
        }

        return new NumberValue(l.Value / r.Value);
    }

    private static OslangValue EvalModulo(OslangValue left, OslangValue right, SourceLocation location)
    {
        if (left is not NumberValue l || right is not NumberValue r)
        {
            throw new OslangRuntimeException(location, $"Invalid operation '%' between {left.TypeName} and {right.TypeName}.");
        }

        if (r.Value == 0)
        {
            throw new OslangRuntimeException(location, "Division by zero.");
        }

        return new NumberValue(l.Value % r.Value);
    }

    private static OslangValue EvalPower(OslangValue left, OslangValue right, SourceLocation location)
    {
        if (left is not NumberValue l || right is not NumberValue r)
        {
            throw new OslangRuntimeException(location, $"Invalid operation '**' between {left.TypeName} and {right.TypeName}.");
        }

        return new NumberValue(Math.Pow(l.Value, r.Value));
    }

    private static OslangValue CompareOp(OslangValue left, OslangValue right, SourceLocation location, string op, Func<double, double, bool> fn)
    {
        if (left is not NumberValue l || right is not NumberValue r)
        {
            throw new OslangRuntimeException(location, $"Invalid operation '{op}' between {left.TypeName} and {right.TypeName}.");
        }

        return BooleanValue.Of(fn(l.Value, r.Value));
    }

    private OslangValue EvalSwitchExpr(SwitchExpr expr, Scope scope)
    {
        var switchValue = Eval(expr.Expression, scope);
        OslangValue? result = null;

        foreach (var caseBranch in expr.Cases)
        {
            var caseValue = Eval(caseBranch.Value, scope);
            if (ValuesEqual(switchValue, caseValue))
            {
                result = Eval(caseBranch.Result, scope);
                break;
            }
        }

        if (result is null && expr.DefaultCase is not null)
        {
            result = Eval(expr.DefaultCase.Result, scope);
        }

        return result ?? OslangValue.Null;
    }

    private OslangValue EvalArrowFunction(ArrowFunctionExpr arrow, Scope scope)
    {
        return CreateArrowFunction(arrow.Parameters, arrow.Body, scope);
    }

    private OslangValue EvalBlockArrowFunction(BlockArrowFunctionExpr arrow, Scope scope)
    {
        return CreateBlockArrowFunction(arrow.Parameters, arrow.Body, scope);
    }

    private OslangValue EvalPostfix(PostfixExpr postfix, Scope scope)
    {
        var operandExpr = postfix.Operand;
        
        if (operandExpr is IdentifierExpr id)
        {
            var variable = scope.ResolveForAssignment(id.Name);
            var oldValue = variable.Value;
            var numericValue = oldValue as NumberValue ?? throw new OslangRuntimeException(postfix.Location, $"Postfix '{postfix.Operator}' requires a NUMBER operand.");
            
            var newValue = postfix.Operator switch
            {
                "++" => new NumberValue(numericValue.Value + 1),
                "--" => new NumberValue(numericValue.Value - 1),
                _ => throw new InvalidOperationException($"Unknown postfix operator '{postfix.Operator}'.")
            };
            
            TypeSystem.Assign(variable, newValue, postfix.Location, $"variable '{id.Name}'");
            return oldValue;
        }
        
        if (operandExpr is IndexExpr ix)
        {
            var array = EvalArray(ix.Array, scope);
            var index = ResolveIndex(Eval(ix.Index, scope), array, ix.Location);
            var oldValue = array.Items[index];
            var numericValue = oldValue as NumberValue ?? throw new OslangRuntimeException(postfix.Location, $"Postfix '{postfix.Operator}' requires a NUMBER element.");
            
            var newValue = postfix.Operator switch
            {
                "++" => new NumberValue(numericValue.Value + 1),
                "--" => new NumberValue(numericValue.Value - 1),
                _ => throw new InvalidOperationException($"Unknown postfix operator '{postfix.Operator}'.")
            };
            
            TypeSystem.AssignArrayElement(array, index, newValue, postfix.Location);
            return oldValue;
        }
        
        if (operandExpr is MemberAccessExpr ma)
        {
            var obj = Eval(ma.Object, scope);
            if (obj is not ObjectValue objectValue)
            {
                throw new OslangRuntimeException(postfix.Location, $"Cannot apply postfix '{postfix.Operator}' to type {obj.TypeName}.");
            }
            
            var instance = objectValue.Instance;
            var classDef = instance.ClassDefinition;
            var prop = classDef.FindProperty(ma.MemberName);
            if (prop is null)
            {
                throw new OslangRuntimeException(postfix.Location, $"Property '{ma.MemberName}' not found in class '{classDef.Name}'.");
            }
            
            CheckMemberAccessVisibility(instance, prop.Visibility, prop.Name, postfix.Location);
            
            if (!instance.PropertyValues.TryGetValue(prop.Name, out var oldValue))
            {
                oldValue = OslangValue.Null;
            }
            var numericValue = oldValue as NumberValue ?? throw new OslangRuntimeException(postfix.Location, $"Postfix '{postfix.Operator}' requires a NUMBER property.");
            
            var newValue = postfix.Operator switch
            {
                "++" => new NumberValue(numericValue.Value + 1),
                "--" => new NumberValue(numericValue.Value - 1),
                _ => throw new InvalidOperationException($"Unknown postfix operator '{postfix.Operator}'.")
            };
            
            instance.PropertyValues[prop.Name] = newValue;
            return oldValue;
        }
        
        throw new OslangRuntimeException(postfix.Location, $"Invalid target for postfix '{postfix.Operator}'. Expected variable, array index, or member access.");
    }

    private FunctionValue CreateArrowFunction(IReadOnlyList<string> parameters, Expr body, Scope scope)
    {
        var capturedScope = scope;
        
        return new FunctionValue((args, location) =>
        {
            if (args.Count != parameters.Count)
            {
                throw new OslangRuntimeException(location, $"Arrow function expects {parameters.Count} argument(s), got {args.Count}.");
            }
            
            var innerScope = new Scope(capturedScope);
            for (var i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var variable = innerScope.DeclareLocal(param);
                TypeSystem.Assign(variable, args[i], location, $"parameter '{param}'");
            }
            
            return Eval(body, innerScope);
        });
    }

    private FunctionValue CreateBlockArrowFunction(IReadOnlyList<string> parameters, IReadOnlyList<Stmt> body, Scope scope)
    {
        var capturedScope = scope;
        
        return new FunctionValue((args, location) =>
        {
            if (args.Count != parameters.Count)
            {
                throw new OslangRuntimeException(location, $"Arrow function expects {parameters.Count} argument(s), got {args.Count}.");
            }
            
            var innerScope = new Scope(capturedScope);
            for (var i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var variable = innerScope.DeclareLocal(param);
                TypeSystem.Assign(variable, args[i], location, $"parameter '{param}'");
            }
            
            try
            {
                foreach (var stmt in body)
                {
                    ExecuteStatement(stmt, innerScope, loopDepth: 0);
                }
            }
            catch (ReturnSignal ret)
            {
                return ret.Value;
            }
            
            return OslangValue.Null;
        });
    }

    private static bool ValuesEqual(OslangValue left, OslangValue right)
    {
        // seção 20: NULL só é igual a NULL.
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
            (EnumValue a, EnumValue b) => a.Equals(b),
            _ => false,
        };
    }

    // ============================================================
    // OSLANG 0.2 - Orientação a objetos
    // ============================================================

    private OslangValue EvalMe(Scope scope)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "ME used outside of a class method.");
        }

        return new ObjectValue(_currentObject);
    }

    private OslangValue EvalBase(Scope scope)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "BASE used outside of a class method.");
        }

        var baseClass = _currentObject.ClassDefinition.BaseClass;
        if (baseClass is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "BASE can only be used in a derived class.");
        }

        return new ObjectValue(_currentObject);
    }

    private OslangValue EvalNamespace(NamespaceExpr ns, Scope scope)
    {
        return ns.NamespaceName.ToUpperInvariant() switch
        {
            "MATH" => OslangValue.Null, // MATH is a namespace marker, actual methods dispatched in EvalMethodCall
            "FILE" => OslangValue.Null, // FILE is a namespace marker
            "DIR" => OslangValue.Null, // DIR is a namespace marker
            "OSL" => OslangValue.Null,  // OSL is a namespace marker, actual methods dispatched in EvalMethodCall
            _ => throw new OslangRuntimeException(ns.Location, $"Unknown namespace '{ns.NamespaceName}'."),
        };
    }

    private OslangValue EvalMemberAccess(MemberAccessExpr expr, Scope scope)
    {
        if (expr.Object is NamespaceExpr ns)
        {
            return CallNamespaceMethod(ns.NamespaceName, expr.MemberName, [], expr.Location);
        }

        var obj = Eval(expr.Object, scope);

        if (obj is ModuleValue module)
        {
            return I18nNamespace.Call(expr.MemberName, [], expr.Location);
        }

        if (obj is EnumTypeValue enumType)
        {
            if (!_enums.TryGetValue(enumType.EnumName, out var members))
            {
                throw new OslangRuntimeException(expr.Location, $"Unknown enum type '{enumType.EnumName}'.");
            }

            var member = members.FirstOrDefault(m => m.MemberName.Equals(expr.MemberName, StringComparison.OrdinalIgnoreCase));
            if (member.MemberName is null)
            {
                throw new OslangRuntimeException(expr.Location, $"Member '{expr.MemberName}' not found in enum '{enumType.EnumName}'.");
            }

            return new EnumValue(member.Value, enumType.EnumName, member.MemberName);
        }

        if (obj is EnumValue enumValue)
        {
            return DispatchEnum(enumValue, expr.MemberName, [], expr.Location);
        }

        if (obj is EnumSetValue enumSet)
        {
            return DispatchEnumSet(enumSet, expr.MemberName, [], expr.Location);
        }

        if (obj is not ObjectValue objectValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot access member '{expr.MemberName}' on type {obj.TypeName}.");
        }

        var classDef = expr.Object is BaseExpr && objectValue.Instance.ClassDefinition.BaseClass is not null
            ? objectValue.Instance.ClassDefinition.BaseClass
            : objectValue.Instance.ClassDefinition;

        var prop = classDef.FindProperty(expr.MemberName);
        if (prop is null)
        {
            throw new OslangRuntimeException(expr.Location, $"Property '{expr.MemberName}' not found in class '{classDef.Name}'.");
        }

        CheckMemberAccessVisibility(objectValue.Instance, prop.Visibility, prop.Name, expr.Location);
        
        if (objectValue.Instance.PropertyValues.TryGetValue(prop.Name, out var value))
        {
            return value;
        }
        return OslangValue.Null;
    }

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

    private OslangValue EvalMethodCall(MethodCallExpr expr, Scope scope)
    {
        if (expr.Object is NamespaceExpr ns)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return CallNamespaceMethod(ns.NamespaceName, expr.MethodName, args, expr.Location);
        }

        var obj = Eval(expr.Object, scope);

        if (obj is ModuleValue module)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return I18nNamespace.Call(expr.MethodName, args, expr.Location);
        }

        if (obj is EnumValue enumValue)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchEnum(enumValue, expr.MethodName, args, expr.Location);
        }

        if (obj is EnumSetValue enumSet)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchEnumSet(enumSet, expr.MethodName, args, expr.Location);
        }

        if (obj is ObjectValue objectValue)
        {
            var classDef = expr.Object is BaseExpr && objectValue.Instance.ClassDefinition.BaseClass is not null
                ? objectValue.Instance.ClassDefinition.BaseClass
                : objectValue.Instance.ClassDefinition;

            var method = classDef.FindMethod(expr.MethodName);
            if (method is null)
            {
                throw new OslangRuntimeException(expr.Location, $"Method '{expr.MethodName}' not found in class '{classDef.Name}'.");
            }

            var declaringClass = FindMethodDeclaringClass(classDef, expr.MethodName);
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return CallMethod(objectValue.Instance, method, declaringClass, args, expr.Location);
        }

        if (obj is StringValue or NumberValue or BooleanValue or ArrayValue)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return PrimitiveMethodDispatcher.Dispatch(obj, expr.MethodName, args, expr.Location);
        }

        if (obj is NullValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot call method '{expr.MethodName}' on NULL.");
        }

        throw new OslangRuntimeException(expr.Location, $"Cannot call method '{expr.MethodName}' on type {obj.TypeName}.");
    }

    private ClassDefinition? FindMethodDeclaringClass(ClassDefinition classDef, string methodName)
    {
        var current = classDef;
        while (current != null)
        {
            if (current.Methods.Any(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            current = current.BaseClass;
        }
        return null;
    }

    private void CheckMemberAccessVisibility(ObjectInstance instance, Visibility memberVisibility, string memberName, SourceLocation location)
    {
        if (memberVisibility == Visibility.Public)
        {
            return;
        }

        var accessingClass = _enclosingClass;
        if (accessingClass is null && _currentObject is not null)
        {
            accessingClass = _currentObject.ClassDefinition;
        }

        var declaringClass = FindDeclaringClass(memberName);

        if (memberVisibility == Visibility.Private)
        {
            if (declaringClass is null || !ReferenceEquals(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PRIVATE in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
        else if (memberVisibility == Visibility.Protected)
        {
            if (declaringClass is null || !IsDerivedFrom(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PROTECTED in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
    }

    private OslangValue EvalNew(NewExpr expr, Scope scope)
    {
        if (!_classes.TryGetValue(expr.ClassName, out var classDef))
        {
            throw new OslangRuntimeException(expr.Location, $"Unknown class '{expr.ClassName}'.");
        }

        var instance = new ObjectInstance(classDef);
        
        InitializeProperties(instance, classDef);
        
        var args = expr.Args.Select(a => Eval(a, scope)).ToList();
        
        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        _currentObject = instance;
        _enclosingClass = classDef;
        
        try
        {
            var constructor = classDef.Constructor;
            if (constructor is not null)
            {
                ExecuteConstructor(instance, constructor, args, classDef, expr.Location);
            }
            else if (classDef.BaseClass is not null && classDef.BaseClass.Constructor is not null)
            {
                ExecuteBaseConstructor(instance, classDef.BaseClass, [], expr.Location);
            }
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
        }

        return new ObjectValue(instance);
    }

    private void InitializeProperties(ObjectInstance instance, ClassDefinition classDef)
    {
        var current = classDef;
        while (current != null)
        {
            foreach (var prop in current.Properties)
            {
                if (!instance.PropertyValues.ContainsKey(prop.Name))
                {
                    instance.PropertyValues[prop.Name] = prop.TypeName is not null 
                        ? TypeSystem.DefaultValueFor(TypeSystem.ParseTypeName(prop.TypeName)) 
                        : OslangValue.Null;
                }
            }
            current = current.BaseClass;
        }
    }

    private void ExecuteBaseConstructor(ObjectInstance instance, ClassDefinition baseClass, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (baseClass.BaseClass is not null)
        {
            ExecuteBaseConstructor(instance, baseClass.BaseClass, args, callLocation);
        }

        var constructor = baseClass.Constructor;
        if (constructor is not null)
        {
            ExecuteConstructor(instance, constructor, args, baseClass, callLocation);
        }
    }

    private void ExecuteConstructor(ObjectInstance instance, ConstructorDefinition constructor, IReadOnlyList<OslangValue> args, ClassDefinition enclosingClass, SourceLocation callLocation)
    {
        if (args.Count != constructor.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Constructor expects {constructor.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        var previousInConstructor = _inConstructor;
        _currentObject = instance;
        _enclosingClass = enclosingClass;
        _inConstructor = true;

        var scope = new Scope(_globals);
        for (var i = 0; i < constructor.Parameters.Count; i++)
        {
            var param = constructor.Parameters[i];
            var variable = scope.DeclareLocal(param.Name);
            if (param.TypeName is not null)
            {
                variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
            }
            TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
        }

        try
        {
            var body = constructor.Body;
            var remainingBody = body;
            
            if (body.Count > 0 && body[0] is BaseCallStmt baseCall)
            {
                var baseArgs = baseCall.Args.Select(a => Eval(a, scope)).ToList();
                if (enclosingClass.BaseClass is not null && enclosingClass.BaseClass.Constructor is not null)
                {
                    ExecuteConstructor(instance, enclosingClass.BaseClass.Constructor, baseArgs, enclosingClass.BaseClass, baseCall.Location);
                }
                else
                {
                    throw new OslangRuntimeException(baseCall.Location, "BASE can only be used in a derived class with a base class constructor.");
                }
                remainingBody = body.Skip(1).ToList();
            }
            else if (enclosingClass.BaseClass is not null && enclosingClass.BaseClass.Constructor is not null)
            {
                ExecuteBaseConstructor(instance, enclosingClass.BaseClass, [], callLocation);
            }
            
            ExecuteBlock(remainingBody, scope, loopDepth: 0);
        }
        catch (ReturnSignal)
        {
            // constructors don't return values, ignore
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
            _inConstructor = previousInConstructor;
        }
    }

    private OslangValue CallMethod(ObjectInstance instance, MethodDefinition method, ClassDefinition? declaringClass, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (args.Count != method.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Method '{method.Name}' expects {method.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        _currentObject = instance;
        _enclosingClass = declaringClass ?? instance.ClassDefinition;

        var scope = new Scope(_globals);
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var variable = scope.DeclareLocal(param.Name);
            if (param.TypeName is not null)
            {
                variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
            }
            TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
        }

        try
        {
            ExecuteBlock(method.Body, scope, loopDepth: 0);
        }
        catch (ReturnSignal ret)
        {
            return ret.Value;
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
        }

        return OslangValue.Null;
    }

    private OslangValue ResolveMemberAccess(ObjectInstance instance, string memberName, SourceLocation location, bool isWrite)
    {
        var classDef = instance.ClassDefinition;
        var prop = classDef.FindProperty(memberName);
        if (prop is not null)
        {
            if (isWrite)
            {
                instance.PropertyValues[prop.Name] = OslangValue.Null; // placeholder, actual assignment handled elsewhere
            }
            if (instance.PropertyValues.TryGetValue(prop.Name, out var value))
            {
                return value;
            }
            return OslangValue.Null;
        }

        var method = classDef.FindMethod(memberName);
        if (method is not null)
        {
            // Return a callable wrapper - for now, just return Null since method calls use MethodCallExpr
            return OslangValue.Null;
        }

        throw new OslangRuntimeException(location, $"Member '{memberName}' not found in class '{classDef.Name}'.");
    }

    private OslangValue ResolveMemberForAssignment(ObjectInstance instance, string memberName, SourceLocation location)
    {
        var classDef = instance.ClassDefinition;
        var prop = classDef.FindProperty(memberName);
        if (prop is null)
        {
            throw new OslangRuntimeException(location, $"Property '{memberName}' not found in class '{classDef.Name}'.");
        }

        return instance.PropertyValues.GetValueOrDefault(prop.Name, OslangValue.Null);
    }
}
