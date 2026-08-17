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
}
