using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Expr ParseNewExpression()
    {
        var start = Current.Location;
        Advance(); // consume NEW
        var classNameTok = Expect(TokenType.Identifier, "Expected class name after NEW.");
        Expect(TokenType.LParen, "Expected '(' after class name in NEW expression.");
        var args = ParseArgList();
        return new NewExpr(classNameTok.Text, args, start);
    }


    private Expr ParseIdentifierOrArrow()
    {
        var tok = Advance();
        
        if (Check(TokenType.DoubleArrow))
        {
            Advance(); // consume =>
            return ParseArrowFunctionBody([tok.Text], tok.Location);
        }
        
        if (Check(TokenType.LParen))
        {
            Advance(); // consume '('
            var args = ParseArgList();
            return new CallExpr(tok.Text, args, tok.Location);
        }

        return new IdentifierExpr(tok.Lexeme, tok.Location);
    }


    private Expr ParseParenOrArrow()
    {
        var start = Current.Location;
        Advance(); // consume '('
        
        if (Check(TokenType.RParen))
        {
            Advance(); // consume ')'
            if (Check(TokenType.DoubleArrow))
            {
                Advance(); // consume =>
                return ParseArrowFunctionBody([], start);
            }
            
            throw new SyntaxException(start, "Empty parentheses are not valid as an expression.");
        }
        
        var paramStart = _pos;
        var paramNames = new List<string>();
        var isArrowParams = false;
        
        if (Current.Type == TokenType.Identifier)
        {
            paramNames.Add(Advance().Text);
            
            while (Check(TokenType.Comma))
            {
                Advance();
                if (Current.Type != TokenType.Identifier)
                {
                    break;
                }
                paramNames.Add(Advance().Text);
            }
            
            if (Check(TokenType.RParen) && CheckNext(TokenType.DoubleArrow))
            {
                isArrowParams = true;
            }
        }
        
        if (isArrowParams)
        {
            Advance(); // consume ')'
            Advance(); // consume =>
            return ParseArrowFunctionBody(paramNames.ToArray(), start);
        }
        
        _pos = paramStart;
        var expr = ParseExpression();
        Expect(TokenType.RParen, "Expected ')' after expression.");
        return expr;
    }


    private Expr ParseArrowFunctionBody(string[] parameters, SourceLocation location)
    {
        if (Check(TokenType.Newline))
        {
            Advance();
            if (Check(TokenType.End))
            {
                var emptyBody = new List<Stmt>();
                Expect(TokenType.End, "Expected END to close empty arrow function block.");
                return new BlockArrowFunctionExpr(parameters, emptyBody, location);
            }
            var blockBody = ParseBlockUntil(TokenType.End);
            Expect(TokenType.End, "Expected END to close arrow function block.");
            return new BlockArrowFunctionExpr(parameters, blockBody, location);
        }
        
        var body = ParseExpression();
        if (Check(TokenType.End))
        {
            Advance(); // consume optional END for expression body
        }
        return new ArrowFunctionExpr(parameters, body, location);
    }


    private Expr ParseArrayLiteral()
    {
        var start = Current.Location;
        Advance(); // '['
        var elements = new List<Expr>();
        SkipNewlines();
        if (!Check(TokenType.RBracket))
        {
            elements.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                SkipNewlines();
                elements.Add(ParseExpression());
            }
        }

        SkipNewlines();
        Expect(TokenType.RBracket, "Expected ']' to close array literal.");
        return new ArrayLiteralExpr(elements, start);
    }

    /// <summary>Assume que o token atual é '(' - consome '(' args ')'.</summary>

    private List<Expr> ParseArgList()
    {
        var args = new List<Expr>();
        if (!Check(TokenType.RParen))
        {
            args.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                args.Add(ParseExpression());
            }
        }

        Expect(TokenType.RParen, "Expected ')' to close argument list.");
        return args;
    }
}
