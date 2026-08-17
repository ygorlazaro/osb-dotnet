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
                Variable? variable;
                if (scope.HasLocal(vt.Name))
                {
                    variable = scope.ResolveForAssignment(vt.Name);
                }
                else if (_currentObject is not null)
                {
                    var prop = _currentObject.ClassDefinition.FindProperty(vt.Name);
                    if (prop is not null)
                    {
                        CheckMemberVisibility(prop.Visibility, prop.Name, vt.Location);
                        _currentObject.PropertyValues[prop.Name] = value;
                        variable = null;
                    }
                    else
                    {
                        variable = scope.ResolveForAssignment(vt.Name);
                    }
                }
                else
                {
                    variable = scope.ResolveForAssignment(vt.Name);
                }

                if (variable is not null)
                {
                    TypeSystem.Assign(variable, value, vt.Location, $"variable '{vt.Name}'");
                }
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
}
