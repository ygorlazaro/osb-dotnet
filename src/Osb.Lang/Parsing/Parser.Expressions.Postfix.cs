using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Expr ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Check(TokenType.LBracket))
            {
                var loc = Current.Location;
                Advance();
                var index = ParseExpression();
                Expect(TokenType.RBracket, "Expected ']' after array index.");
                expr = new IndexExpr(expr, index, loc);
            }
            else if (Check(TokenType.Dot))
            {
                var loc = Current.Location;
                Advance(); // consume '.'
                var memberTok = ParseMemberName();
                
                if (Check(TokenType.LParen))
                {
                    Advance(); // consume '('
                    var args = ParseArgList();
                    expr = new MethodCallExpr(expr, memberTok, args, loc);
                }
                else
                {
                    expr = new MemberAccessExpr(expr, memberTok, loc);
                }
            }
            else if (Check(TokenType.PlusPlus) || Check(TokenType.MinusMinus))
            {
                var op = Current.Type == TokenType.PlusPlus ? "++" : "--";
                var loc = Current.Location;
                Advance();
                expr = new PostfixExpr(op, expr, loc);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private static readonly HashSet<TokenType> BuiltinCallKeywords =
    [
        TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
        TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf,
    ];


    private Expr ParsePrimary()
    {
        var tok = Current;

        switch (tok.Type)
        {
            case TokenType.NumberLiteral:
                Advance();
                return new NumberLiteralExpr(tok.NumberValue, tok.Location);
            case TokenType.StringLiteral:
            {
                Advance();
                return new StringLiteralExpr(tok.StringValue!, tok.Location);
            }
            case TokenType.True:
                Advance();
                return new BooleanLiteralExpr(true, tok.Location);
            case TokenType.False:
                Advance();
                return new BooleanLiteralExpr(false, tok.Location);
            case TokenType.Null:
                Advance();
                return new NullLiteralExpr(tok.Location);
            case TokenType.LParen:
                return ParseParenOrArrow();
            case TokenType.LBracket:
                return ParseArrayLiteral();
            case TokenType.Me:
                Advance();
                return new MeExpr(tok.Location);
            case TokenType.Base:
                Advance();
                return new BaseExpr(tok.Location);
            case TokenType.New:
                return ParseNewExpression();
            case TokenType.Identifier:
                return ParseIdentifierOrArrow();
            case TokenType.Math:
                Advance();
                return new NamespaceExpr("MATH", tok.Location);
            case TokenType.File:
                Advance();
                return new NamespaceExpr("FILE", tok.Location);
            case TokenType.Dir:
                Advance();
                return new NamespaceExpr("DIR", tok.Location);
            case TokenType.Osl:
                Advance();
                return new NamespaceExpr("OSL", tok.Location);
            case TokenType.Osb:
                Advance();
                return new NamespaceExpr("OSB", tok.Location);
        }

        if (BuiltinCallKeywords.Contains(tok.Type))
        {
            Advance();
            Expect(TokenType.LParen, $"Expected '(' after {tok.Text}.");
            var args = ParseArgList();
            return new CallExpr(tok.Text, args, tok.Location);
        }

        throw new SyntaxException(tok.Location, $"Unexpected token '{tok.Lexeme}' in expression.");
    }
}
