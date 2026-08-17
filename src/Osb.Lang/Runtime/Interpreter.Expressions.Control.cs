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
}
