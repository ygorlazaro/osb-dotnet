using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Expr ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (CheckAny(TokenType.Plus, TokenType.Minus))
        {
            var op = Current.Type == TokenType.Plus ? "+" : "-";
            var loc = Current.Location;
            Advance();
            var right = ParseMultiplicative();
            left = new BinaryExpr(op, left, right, loc);
        }

        return left;
    }


    private Expr ParseMultiplicative()
    {
        var left = ParseExponentiation();
        while (CheckAny(TokenType.Star, TokenType.Slash, TokenType.Percent, TokenType.Mod))
        {
            var op = Current.Type switch { TokenType.Star => "*", TokenType.Slash => "/", TokenType.Percent => "%", TokenType.Mod => "MOD", _ => "*" };
            var loc = Current.Location;
            Advance();
            var right = ParseExponentiation();
            left = new BinaryExpr(op, left, right, loc);
        }

        return left;
    }


    private Expr ParseExponentiation()
    {
        var left = ParseUnary();
        if (Check(TokenType.StarStar))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseExponentiation(); // right-associative
            left = new BinaryExpr("**", left, right, loc);
        }

        return left;
    }


    private Expr ParseUnary()
    {
        if (Check(TokenType.Not))
        {
            var loc = Current.Location;
            Advance();
            return new UnaryExpr("NOT", ParseUnary(), loc);
        }

        if (Check(TokenType.Minus))
        {
            var loc = Current.Location;
            Advance();
            return new UnaryExpr("-", ParseUnary(), loc);
        }

        return ParseSwitchOrPostfix();
    }


    private Expr ParseSwitchOrPostfix()
    {
        if (Check(TokenType.Switch))
        {
            return ParseSwitchExpression();
        }

        return ParsePostfix();
    }


    private Expr ParseSwitchExpression()
    {
        var start = Current.Location;
        Advance(); // consume SWITCH
        var expression = ParseExpression();
        
        var cases = new List<CaseBranch>();
        DefaultBranch? defaultCase = null;

        while (!IsAtEnd && !Check(TokenType.End) && !Check(TokenType.Elif) && !Check(TokenType.Else))
        {
            if (Check(TokenType.Newline))
            {
                var savedPos = _pos;
                SkipNewlines();
                
                if (Check(TokenType.Case) || Check(TokenType.Default))
                {
                    continue;
                }
                
                _pos = savedPos;
                break;
            }
            
            if (Check(TokenType.Case))
            {
                cases.Add(ParseCaseBranch());
            }
            else if (Check(TokenType.Default))
            {
                if (defaultCase is not null)
                {
                    throw new SyntaxException(Current.Location, "Duplicate DEFAULT in switch expression.");
                }

                defaultCase = ParseDefaultBranch();
            }
            else
            {
                break;
            }
        }

        return new SwitchExpr(expression, cases, defaultCase, start);
    }


    private CaseBranch ParseCaseBranch()
    {
        var start = Current.Location;
        Advance(); // consume CASE
        var value = ParseExpression();
        Expect(TokenType.DoubleArrow, "Expected '=>' after CASE value in switch expression.");
        var result = ParseExpression();
        return new CaseBranch(value, result, start);
    }


    private DefaultBranch ParseDefaultBranch()
    {
        var start = Current.Location;
        Advance(); // consume DEFAULT
        Expect(TokenType.DoubleArrow, "Expected '=>' after DEFAULT in switch expression.");
        var result = ParseExpression();
        return new DefaultBranch(result, start);
    }
}
