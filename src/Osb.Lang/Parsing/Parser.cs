using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

/// <summary>
/// Parser recursivo-descendente de OSLANG 0.1, implementando a precedência de
/// operadores da seção 24 e a gramática das seções 15-43.
///
/// Decisões de design não explicitadas na especificação:
///
/// 1. "END FUNCTION" fecha uma função, mas o parser também aceita "END" sozinho
///    fechando uma função (o FUNCTION final é opcional) - mais tolerante, sem
///    contradizer nenhum exemplo da especificação, que sempre usa "END FUNCTION".
///
/// 2. Comparações (&lt; &gt; &lt;= &gt;= = &lt;&gt;) são parseadas associando à
///    esquerda, permitindo (embora sem necessidade prática) encadeamentos como
///    "A &lt; B &lt; C" - o resultado desse encadeamento é decidido em runtime
///    pelas regras normais de tipo (comparar um BOOLEAN com um NUMBER dá erro).
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    private Token Current => _tokens[_pos];

    private bool IsAtEnd => Current.Type == TokenType.Eof;

    private Token Advance()
    {
        var tok = Current;
        if (!IsAtEnd)
        {
            _pos++;
        }

        return tok;
    }

    private bool Check(TokenType type) => Current.Type == type;

    private bool CheckAny(params TokenType[] types) => Array.IndexOf(types, Current.Type) >= 0;

    private Token Expect(TokenType type, string message)
    {
        if (!Check(type))
        {
            throw new SyntaxException(Current.Location, message + $" Found '{Current.Lexeme}'.");
        }

        return Advance();
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline))
        {
            Advance();
        }
    }

    // ============================================================
    // Programa / funções
    // ============================================================

    public OslangProgram Parse()
    {
        var functions = new List<FunctionDecl>();
        SkipNewlines();
        while (!IsAtEnd)
        {
            functions.Add(ParseFunctionDecl());
            SkipNewlines();
        }

        return new OslangProgram(functions);
    }

    private FunctionDecl ParseFunctionDecl()
    {
        var start = Current.Location;
        Expect(TokenType.Function, "Expected FUNCTION declaration.");
        var nameTok = Expect(TokenType.Identifier, "Expected function name.");

        Expect(TokenType.LParen, "Expected '(' after function name.");
        var parameters = new List<ParameterDecl>();
        if (!Check(TokenType.RParen))
        {
            parameters.Add(ParseParameter());
            while (Check(TokenType.Comma))
            {
                Advance();
                parameters.Add(ParseParameter());
            }
        }

        Expect(TokenType.RParen, "Expected ')' after parameter list.");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close function body.");
        if (Check(TokenType.Function))
        {
            Advance(); // "END FUNCTION" - a palavra FUNCTION final é opcional (decisão 1 acima)
        }

        return new FunctionDecl(nameTok.Text, parameters, body, start);
    }

    private ParameterDecl ParseParameter()
    {
        var nameTok = Expect(TokenType.Identifier, "Expected parameter name.");
        var typeName = TryParseTypeAnnotation();
        return new ParameterDecl(nameTok.Text, typeName, nameTok.Location);
    }

    /// <summary>NUMBER, STRING ou BOOLEAN como anotação de tipo de VAR/parâmetro; null se ausente.</summary>
    private string? TryParseTypeAnnotation()
    {
        if (CheckAny(TokenType.Number, TokenType.String, TokenType.Boolean))
        {
            return Advance().Text;
        }

        return null;
    }

    // ============================================================
    // Blocos
    // ============================================================

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
            TokenType.Input => ParseInput(),
            TokenType.Clear => new ClearStmt(Advance().Location),
            TokenType.Identifier => ParseIdentifierLedStatement(),
            _ => throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Lexeme}' at start of statement."),
        };
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

        return new ExpressionStmt(expr, start);
    }

    private static AssignTarget ToAssignTarget(Expr expr) => expr switch
    {
        IdentifierExpr id => new VariableTarget(id.Name, id.Location),
        IndexExpr ix => new IndexTarget(ix.Array, ix.Index, ix.Location),
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

    private Expr ParseExpression() => ParseOr();

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
        var left = ParseComparison();
        while (Check(TokenType.And))
        {
            var loc = Current.Location;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpr("AND", left, right, loc);
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
        var left = ParseUnary();
        while (CheckAny(TokenType.Star, TokenType.Slash, TokenType.Percent))
        {
            var op = Current.Type switch { TokenType.Star => "*", TokenType.Slash => "/", _ => "%" };
            var loc = Current.Location;
            Advance();
            var right = ParseUnary();
            left = new BinaryExpr(op, left, right, loc);
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

        return ParsePostfix();
    }

    private Expr ParsePostfix()
    {
        var expr = ParsePrimary();
        while (Check(TokenType.LBracket))
        {
            var loc = Current.Location;
            Advance();
            var index = ParseExpression();
            Expect(TokenType.RBracket, "Expected ']' after array index.");
            expr = new IndexExpr(expr, index, loc);
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
                Advance();
                return new StringLiteralExpr(tok.StringValue!, tok.Location);
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
            {
                Advance();
                var inner = ParseExpression();
                Expect(TokenType.RParen, "Expected ')'.");
                return inner;
            }

            case TokenType.LBracket:
                return ParseArrayLiteral();
            case TokenType.Identifier:
                return ParseIdentifierOrCall();
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

    private Expr ParseIdentifierOrCall()
    {
        var tok = Advance();
        if (Check(TokenType.LParen))
        {
            Advance(); // consume '('
            var args = ParseArgList();
            return new CallExpr(tok.Text, args, tok.Location);
        }

        return new IdentifierExpr(tok.Text, tok.Location);
    }

    private Expr ParseArrayLiteral()
    {
        var start = Current.Location;
        Advance(); // '['
        var elements = new List<Expr>();
        if (!Check(TokenType.RBracket))
        {
            elements.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                elements.Add(ParseExpression());
            }
        }

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
