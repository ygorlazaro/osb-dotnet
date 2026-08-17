using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
    private Stmt ParseSwitchStatement()
    {
        var start = Current.Location;
        Advance(); // consume SWITCH
        var expression = ParseExpression();
        var cases = new List<CaseClause>();
        DefaultClause? defaultCase = null;

        while (!IsAtEnd && !Check(TokenType.End))
        {
            if (Check(TokenType.Case))
            {
                cases.Add(ParseCaseClause());
            }
            else if (Check(TokenType.Default))
            {
                if (defaultCase is not null)
                {
                    throw new SyntaxException(Current.Location, "Duplicate DEFAULT clause in SWITCH.");
                }

                defaultCase = ParseDefaultClause();
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Lexeme}' in SWITCH. Expected CASE, DEFAULT, or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close SWITCH.");
        return new SwitchStmt(expression, cases, defaultCase, start);
    }


    private CaseClause ParseCaseClause()
    {
        var start = Current.Location;
        Advance(); // consume CASE
        var value = ParseExpression();
        SkipNewlines();
        var body = ParseBlockUntil(TokenType.Case, TokenType.Default, TokenType.End);
        return new CaseClause(value, body, start);
    }


    private DefaultClause ParseDefaultClause()
    {
        var start = Current.Location;
        Advance(); // consume DEFAULT
        SkipNewlines();
        var body = ParseBlockUntil(TokenType.Case, TokenType.End);
        return new DefaultClause(body, start);
    }

    /// <summary>NUMBER, STRING ou BOOLEAN como anotação de tipo de VAR/parâmetro; null se ausente.</summary>

    private List<Stmt> ParseBlockUntil(params TokenType[] terminators)
    {
        var stmts = new List<Stmt>();
        SkipNewlines();
        while (!IsAtEnd && Array.IndexOf(terminators, Current.Type) < 0)
        {
            stmts.Add(ParseStatement());

            if (Check(TokenType.Newline))
            {
                SkipNewlines();
            }
            else if (Array.IndexOf(terminators, Current.Type) >= 0 || IsAtEnd)
            {
                // ok - fim do bloco ou fim do arquivo logo após o statement
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Expected end of line, but found '{Current.Lexeme}'.");
            }
        }

        return stmts;
    }

    // ============================================================
    // Statements
    // ============================================================
}
