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

        if (_enumTypes.TryGetValue(id.Name, out var enumType))
        {
            return enumType;
        }

        throw new OslangRuntimeException(id.Location, $"Undefined variable '{id.Name}'.");
    }
}
