using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    public Expr ParseExpression() => ParseOr();


    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Check(TokenType.Or))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseAnd();
            left = new BinaryExpr("OR", left, right, loc);
        }

        return left;
    }


    private Expr ParseAnd()
    {
        var left = ParseEnumSet();
        while (Check(TokenType.And))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseEnumSet();
            left = new BinaryExpr("AND", left, right, loc);
        }

        return left;
    }


    private Expr ParseEnumSet()
    {
        var left = ParseComparison();
        while (Check(TokenType.Pipe))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseComparison();
            left = new EnumSetExpr(left, right, loc);
        }

        return left;
    }

    private static readonly Dictionary<TokenType, string> ComparisonOps = new()
    {
        [TokenType.Equal] = "=",
        [TokenType.NotEqual] = "<>",
        [TokenType.Less] = "<",
        [TokenType.Greater] = ">",
        [TokenType.LessEqual] = "<=",
        [TokenType.GreaterEqual] = ">=",
    };


    private Expr ParseComparison()
    {
        var left = ParseAdditive();
        while (ComparisonOps.TryGetValue(Current.Type, out var op))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseAdditive();
            left = new BinaryExpr(op, left, right, loc);
        }

        return left;
    }
}
