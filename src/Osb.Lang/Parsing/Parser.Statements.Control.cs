using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Stmt ParseStatement()
    {
        return Current.Type switch
        {
            TokenType.Var => ParseVarDecl(),
            TokenType.Global => ParseGlobalDecl(),
            TokenType.If => ParseIf(),
            TokenType.For => ParseFor(),
            TokenType.While => ParseWhile(),
            TokenType.Do => ParseDoWhile(),
            TokenType.Break => new BreakStmt(Advance().Location),
            TokenType.Continue => new ContinueStmt(Advance().Location),
            TokenType.Return => ParseReturn(),
            TokenType.Try => ParseTryCatch(),
            TokenType.Print => ParsePrint(),
            TokenType.Show => ParseShow(),
            TokenType.Input => ParseInput(),
            TokenType.Clear => new ClearStmt(Advance().Location),
            TokenType.Base => ParseBaseCall(),
            TokenType.Switch => ParseSwitchStatement(),
            TokenType.Enum => ParseEnumDecl(),
            TokenType.Identifier => ParseIdentifierLedStatement(),
            TokenType.Me => ParseMeLedStatement(),
            TokenType.Math => ParseNamespaceLedStatement(TokenType.Math, "MATH"),
            TokenType.File => ParseNamespaceLedStatement(TokenType.File, "FILE"),
            TokenType.Dir => ParseNamespaceLedStatement(TokenType.Dir, "DIR"),
            TokenType.Osl => ParseNamespaceLedStatement(TokenType.Osl, "OSL"),
            _ => throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Lexeme}' at start of statement."),
        };
    }


    private Stmt ParseBaseCall()
    {
        var start = Current.Location;
        Advance(); // consume BASE
        Expect(TokenType.LParen, "Expected '(' after BASE.");
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
        Expect(TokenType.RParen, "Expected ')' to close BASE argument list.");
        return new BaseCallStmt(args, start);
    }


    private Stmt ParseMeLedStatement()
    {
        var start = Current.Location;
        Advance(); // consume ME
        
        var expr = ParsePostfixStarting(new MeExpr(start));

        if (Check(TokenType.Equal))
        {
            Advance();
            var value = ParseExpression();
            var target = ToAssignTarget(expr);
            return new AssignStmt(target, value, start);
        }

        if (Check(TokenType.PlusEqual))
        {
            Advance();
            var right = ParseExpression();
            var target = ToAssignTarget(expr);
            var addExpr = new BinaryExpr("+", expr, right, start);
            return new AssignStmt(target, addExpr, start);
        }

        return new ExpressionStmt(expr, start);
    }


    private Stmt ParseNamespaceLedStatement(TokenType nsType, string nsName)
    {
        var start = Current.Location;
        Advance(); // consume namespace keyword
        var expr = ParsePostfixStarting(new NamespaceExpr(nsName, start));
        return new ExpressionStmt(expr, start);
    }


    private Expr ParsePostfixStarting(Expr initial)
    {
        var expr = initial;
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


    private Stmt ParseVarDecl()
    {
        var start = Current.Location;
        Advance(); // VAR
        var nameTok = Expect(TokenType.Identifier, "Expected variable name after VAR.");
        var typeName = TryParseTypeAnnotation();
        return new VarDeclStmt(nameTok.Text, typeName, start);
    }


    private Stmt ParseGlobalDecl()
    {
        var start = Current.Location;
        Advance(); // GLOBAL
        var nameTok = Expect(TokenType.Identifier, "Expected variable name after GLOBAL.");
        Expect(TokenType.Equal, "GLOBAL declarations require an initializer, e.g. GLOBAL Name = expression.");
        var value = ParseExpression();
        return new GlobalDeclStmt(nameTok.Text, value, start);
    }
}
