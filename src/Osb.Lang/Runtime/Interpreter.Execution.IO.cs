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
}
