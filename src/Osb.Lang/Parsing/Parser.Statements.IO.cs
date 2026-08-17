using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Stmt ParseIdentifierLedStatement()
    {
        var start = Current.Location;
        var expr = ParsePostfix();

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


    private static AssignTarget ToAssignTarget(Expr expr) => expr switch
    {
        IdentifierExpr id => new VariableTarget(id.Name, id.Location),
        IndexExpr ix => new IndexTarget(ix.Array, ix.Index, ix.Location),
        MemberAccessExpr ma => new MemberTarget(ma.Object, ma.MemberName, ma.Location),
        _ => throw new SyntaxException(expr.Location, "Invalid assignment target."),
    };


    private Stmt ParsePrint()
    {
        var start = Current.Location;
        Advance(); // PRINT

        var expressions = new List<Expr>();
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            expressions.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                expressions.Add(ParseExpression());
            }
        }

        return new PrintStmt(expressions, start);
    }


    private Stmt ParseShow()
    {
        var start = Current.Location;
        Advance(); // SHOW

        var expressions = new List<Expr>();
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            expressions.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                expressions.Add(ParseExpression());
            }
        }

        return new ShowStmt(expressions, start);
    }


    private Stmt ParseInput()
    {
        var start = Current.Location;
        Advance(); // INPUT
        var nameTok = Expect(TokenType.Identifier, "Expected variable name after INPUT.");
        return new InputStmt(nameTok.Text, start);
    }


    private Stmt ParseIf()
    {
        var start = Current.Location;
        Advance(); // IF
        var condition = ParseExpression();
        Expect(TokenType.Then, "Expected THEN after IF condition.");

        var thenBody = ParseBlockUntil(TokenType.Elif, TokenType.Else, TokenType.End);

        var elifBranches = new List<ElifBranch>();
        while (Check(TokenType.Elif))
        {
            Advance();
            var elifCondition = ParseExpression();
            Expect(TokenType.Then, "Expected THEN after ELIF condition.");
            var elifBody = ParseBlockUntil(TokenType.Elif, TokenType.Else, TokenType.End);
            elifBranches.Add(new ElifBranch(elifCondition, elifBody));
        }

        List<Stmt>? elseBody = null;
        if (Check(TokenType.Else))
        {
            Advance();
            elseBody = ParseBlockUntil(TokenType.End);
        }

        Expect(TokenType.End, "Expected END to close IF.");
        if (Check(TokenType.If))
        {
            Advance(); // optional "END IF"
        }
        return new IfStmt(condition, thenBody, elifBranches, elseBody, start);
    }


    private Stmt ParseFor()
    {
        var start = Current.Location;
        Advance(); // FOR
        var nameTok = Expect(TokenType.Identifier, "Expected loop variable name after FOR.");
        Expect(TokenType.Equal, "Expected '=' after FOR loop variable.");
        var from = ParseExpression();
        Expect(TokenType.To, "Expected TO in FOR statement.");
        var to = ParseExpression();

        Expr? step = null;
        if (Check(TokenType.Step))
        {
            Advance();
            step = ParseExpression();
        }

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close FOR.");
        return new ForStmt(nameTok.Text, from, to, step, body, start);
    }


    private Stmt ParseWhile()
    {
        var start = Current.Location;
        Advance(); // WHILE
        var condition = ParseExpression();
        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close WHILE.");
        return new WhileStmt(condition, body, start);
    }


    private Stmt ParseDoWhile()
    {
        var start = Current.Location;
        Advance(); // DO
        Expect(TokenType.While, "Expected WHILE after DO.");
        var condition = ParseExpression();
        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close DO WHILE.");
        return new DoWhileStmt(condition, body, start);
    }


    private Stmt ParseReturn()
    {
        var start = Current.Location;
        Advance(); // RETURN
        if (Check(TokenType.Newline) || IsAtEnd || CheckAny(TokenType.End, TokenType.Elif, TokenType.Else, TokenType.Catch))
        {
            return new ReturnStmt(null, start);
        }

        var value = ParseExpression();
        return new ReturnStmt(value, start);
    }


    private Stmt ParseTryCatch()
    {
        var start = Current.Location;
        Advance(); // TRY
        var tryBody = ParseBlockUntil(TokenType.Catch);
        Expect(TokenType.Catch, "Expected CATCH after TRY block.");
        var catchVarTok = Expect(TokenType.Identifier, "Expected error variable name after CATCH (conventionally ERR).");
        var catchBody = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close TRY/CATCH.");
        return new TryCatchStmt(tryBody, catchVarTok.Text, catchBody, start);
    }

    // ============================================================
    // Expressões (precedência - seção 24)
    // ============================================================
}
