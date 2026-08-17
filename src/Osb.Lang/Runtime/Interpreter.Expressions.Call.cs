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
}
