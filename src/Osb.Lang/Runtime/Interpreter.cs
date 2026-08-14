using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;

namespace Osb.Lang.Runtime;

/// <summary>
/// Interpretador tree-walking de OSLANG 0.1/0.2.
/// </summary>
internal sealed class Interpreter
{
    private readonly Dictionary<string, FunctionDecl> _functions = new();
    private readonly Dictionary<string, Variable> _globals = new();
    private readonly Dictionary<string, ClassDefinition> _classes = new();
    private readonly Dictionary<string, InterfaceDefinition> _interfaces = new();
    private readonly ExtensionRegistry _extensions;
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly Action? _clear;
    private ObjectInstance? _currentObject;

    public Interpreter(OslangProgram program, ExtensionRegistry extensions, TextWriter output, TextReader input, Action? clear)
    {
        _extensions = extensions;
        _output = output;
        _input = input;
        _clear = clear;

        foreach (var fn in program.Functions)
        {
            if (!_functions.TryAdd(fn.Name, fn))
            {
                throw new SemanticException(fn.Location, $"Function '{fn.Name}' is already declared.");
            }
        }

        if (!_functions.TryGetValue("MAIN", out var main))
        {
            throw new SemanticException(SourceLocation.Unknown, "Program has no FUNCTION MAIN().");
        }

        if (main.Parameters.Count != 0)
        {
            throw new SemanticException(main.Location, "FUNCTION MAIN() must not declare any parameters.");
        }
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

    public OslangValue Run() => CallFunction("MAIN", [], SourceLocation.Unknown);

    // ============================================================
    // Chamadas de função
    // ============================================================

    private OslangValue CallFunction(string name, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (_functions.TryGetValue(name, out var decl))
        {
            return CallUserFunction(decl, args, callLocation);
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

    private void ExecuteBlock(IReadOnlyList<Stmt> statements, Scope scope, int loopDepth)
    {
        foreach (var stmt in statements)
        {
            ExecuteStatement(stmt, scope, loopDepth);
        }
    }

    private void ExecuteStatement(Stmt stmt, Scope scope, int loopDepth)
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
                if (loopDepth == 0)
                {
                    throw new OslangRuntimeException(b.Location, "BREAK used outside of a loop.");
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
            default:
                throw new InvalidOperationException($"Unknown statement node {stmt.GetType().Name}.");
        }
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

    private void ExecutePrint(PrintStmt p, Scope scope)
    {
        var text = string.Concat(p.Expressions.Select(e => Conversions.ToDisplayString(Eval(e, scope), e.Location)));
        _output.WriteLine(text);
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

    // ============================================================
    // Expressões
    // ============================================================

    private OslangValue Eval(Expr expr, Scope scope) => expr switch
    {
        NumberLiteralExpr n => new NumberValue(n.Value),
        StringLiteralExpr s => new StringValue(s.Value),
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
        UnaryExpr u => EvalUnary(u, scope),
        BinaryExpr b => EvalBinary(b, scope),
        _ => throw new InvalidOperationException($"Unknown expression node {expr.GetType().Name}."),
    };

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
        if (variable is null)
        {
            throw new OslangRuntimeException(id.Location, $"Undefined variable '{id.Name}'.");
        }

        return variable.Value;
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
        return CallFunction(call.Name, args, call.Location);
    }

    private OslangValue EvalUnary(UnaryExpr u, Scope scope)
    {
        if (u.Op == "NOT")
        {
            return BooleanValue.Of(!Conversions.IsTruthy(Eval(u.Operand, scope)));
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

    private static OslangValue CompareOp(OslangValue left, OslangValue right, SourceLocation location, string op, Func<double, double, bool> fn)
    {
        if (left is not NumberValue l || right is not NumberValue r)
        {
            throw new OslangRuntimeException(location, $"Invalid operation '{op}' between {left.TypeName} and {right.TypeName}.");
        }

        return BooleanValue.Of(fn(l.Value, r.Value));
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

    private OslangValue EvalMemberAccess(MemberAccessExpr expr, Scope scope)
    {
        var obj = Eval(expr.Object, scope);
        if (obj is not ObjectValue objectValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot access member '{expr.MemberName}' on type {obj.TypeName}.");
        }

        return ResolveMemberAccess(objectValue.Instance, expr.MemberName, expr.Location, isWrite: false);
    }

    private OslangValue EvalMethodCall(MethodCallExpr expr, Scope scope)
    {
        var obj = Eval(expr.Object, scope);
        if (obj is not ObjectValue objectValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot call method '{expr.MethodName}' on type {obj.TypeName}.");
        }

        var method = objectValue.Instance.ClassDefinition.FindMethod(expr.MethodName);
        if (method is null)
        {
            throw new OslangRuntimeException(expr.Location, $"Method '{expr.MethodName}' not found in class '{objectValue.Instance.ClassName}'.");
        }

        var args = expr.Args.Select(a => Eval(a, scope)).ToList();
        return CallMethod(objectValue.Instance, method, args, expr.Location);
    }

    private OslangValue EvalNew(NewExpr expr, Scope scope)
    {
        if (!_classes.TryGetValue(expr.ClassName, out var classDef))
        {
            throw new OslangRuntimeException(expr.Location, $"Unknown class '{expr.ClassName}'.");
        }

        var instance = new ObjectInstance(classDef);
        
        var args = expr.Args.Select(a => Eval(a, scope)).ToList();
        
        var previousObject = _currentObject;
        _currentObject = instance;
        
        try
        {
            if (classDef.BaseClass is not null)
            {
                ExecuteBaseConstructor(instance, classDef.BaseClass, [], expr.Location);
            }
            
            var constructor = classDef.Constructor;
            if (constructor is not null)
            {
                ExecuteConstructor(instance, constructor, args, expr.Location);
            }
        }
        finally
        {
            _currentObject = previousObject;
        }

        return new ObjectValue(instance);
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
            ExecuteConstructor(instance, constructor, args, callLocation);
        }
    }

    private void ExecuteConstructor(ObjectInstance instance, ConstructorDefinition constructor, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (args.Count != constructor.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Constructor expects {constructor.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        _currentObject = instance;

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
            ExecuteBlock(constructor.Body, scope, loopDepth: 0);
        }
        catch (ReturnSignal)
        {
            // constructors don't return values, ignore
        }
        finally
        {
            _currentObject = previousObject;
        }
    }

    private OslangValue CallMethod(ObjectInstance instance, MethodDefinition method, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (args.Count != method.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Method '{method.Name}' expects {method.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        _currentObject = instance;

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
