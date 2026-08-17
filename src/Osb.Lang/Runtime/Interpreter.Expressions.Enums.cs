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
}
