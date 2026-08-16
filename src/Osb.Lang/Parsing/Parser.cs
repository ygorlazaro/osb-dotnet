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

    private bool CheckNext(TokenType type)
    {
        if (_pos + 1 >= _tokens.Count)
        {
            return false;
        }

        return _tokens[_pos + 1].Type == type;
    }

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
        var classes = new List<ClassDecl>();
        var interfaces = new List<InterfaceDecl>();
        var usings = new List<UsingDecl>();
        var events = new List<EventDecl>();
        var enums = new List<EnumDecl>();
        SkipNewlines();
        while (!IsAtEnd)
        {
            if (Check(TokenType.Using))
            {
                usings.Add(ParseUsingDecl());
            }
            else if (Check(TokenType.Function))
            {
                functions.Add(ParseFunctionDecl());
            }
            else if (Check(TokenType.Class))
            {
                classes.Add(ParseClassDecl());
            }
            else if (Check(TokenType.Interface))
            {
                interfaces.Add(ParseInterfaceDecl());
            }
            else if (Check(TokenType.Event))
            {
                events.Add(ParseEventDecl());
            }
            else if (Check(TokenType.Enum))
            {
                enums.Add(ParseEnumDecl());
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Expected USING, FUNCTION, CLASS, INTERFACE, EVENT, or ENUM at top level. Found '{Current.Lexeme}'.");
            }
            SkipNewlines();
        }

        return new OslangProgram(functions, classes, interfaces, usings, events, enums);
    }

    private FunctionDecl ParseFunctionDecl()
    {
        var start = Current.Location;
        Expect(TokenType.Function, "Expected FUNCTION declaration.");
        var nameTok = ParseNameToken("Expected function name.");

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
            Advance(); // "END FUNCTION" - a palavra FUNCTION final é opcional
        }

        return new FunctionDecl(nameTok.Text, parameters, body, start);
    }

    private Visibility ParseVisibility()
    {
        if (Check(TokenType.Public))
        {
            Advance();
            return Visibility.Public;
        }

        if (Check(TokenType.Protected))
        {
            Advance();
            return Visibility.Protected;
        }

        if (Check(TokenType.Private))
        {
            Advance();
            return Visibility.Private;
        }

        return Visibility.Public;
    }

    private ClassDecl ParseClassDecl()
    {
        var start = Current.Location;
        Expect(TokenType.Class, "Expected CLASS keyword.");
        var nameTok = Expect(TokenType.Identifier, "Expected class name.");

        var inheritedNames = new List<string>();

        if (Check(TokenType.Colon))
        {
            Advance(); // consume ':'
            inheritedNames.Add(Expect(TokenType.Identifier, "Expected base class or interface name after ':'.").Text);

            while (Check(TokenType.Comma))
            {
                Advance();
                inheritedNames.Add(Expect(TokenType.Identifier, "Expected interface name after ','.").Text);
            }
        }

        var members = new List<MemberDecl>();
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            var visibility = ParseVisibility();
            if (Check(TokenType.Var))
            {
                members.Add(ParsePropertyDecl(visibility));
            }
            else if (Check(TokenType.Constructor))
            {
                members.Add(ParseConstructorDecl(visibility));
            }
            else if (Check(TokenType.Function))
            {
                Advance(); // consume FUNCTION keyword for class method
                members.Add(ParseMethodDecl(visibility));
            }
            else if (Check(TokenType.Identifier))
            {
                members.Add(ParseMethodDecl(visibility));
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Lexeme}' in class body. Expected VAR, CONSTRUCTOR, FUNCTION, method name, or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close class.");
        if (Check(TokenType.Class))
        {
            Advance(); // optional "END CLASS"
        }

        return new ClassDecl(nameTok.Text, [], inheritedNames, members, start);
    }

    private InterfaceDecl ParseInterfaceDecl()
    {
        var start = Current.Location;
        Expect(TokenType.Interface, "Expected INTERFACE keyword.");
        var nameTok = Expect(TokenType.Identifier, "Expected interface name.");

        var members = new List<MemberDecl>();
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            if (Check(TokenType.Newline))
            {
                Advance();
                continue;
            }

            // Interface members don't have visibility (implicitly PUBLIC)
            if (Check(TokenType.Var))
            {
                members.Add(ParsePropertyDecl(Visibility.Public));
            }
            else if (Check(TokenType.Identifier))
            {
                members.Add(ParseInterfaceMethodDecl());
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Lexeme}' in interface body.");
            }
        }

        Expect(TokenType.End, "Expected END to close interface.");
        if (Check(TokenType.Interface))
        {
            Advance(); // optional "END INTERFACE"
        }

        return new InterfaceDecl(nameTok.Text, members, start);
    }

    private MethodDecl ParseInterfaceMethodDecl()
    {
        var start = Current.Location;
        var nameTok = Expect(TokenType.Identifier, "Expected method name.");

        Expect(TokenType.LParen, "Expected '(' after method name.");
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

        return new MethodDecl(nameTok.Text, parameters, [], Visibility.Public, start);
    }

    private PropertyDecl ParsePropertyDecl(Visibility visibility)
    {
        var start = Current.Location;
        Advance(); // VAR
        var nameTok = Expect(TokenType.Identifier, "Expected property name after VAR.");
        var typeName = TryParseTypeAnnotation();
        return new PropertyDecl(nameTok.Text, typeName, visibility, start);
    }

    private MethodDecl ParseMethodDecl(Visibility visibility)
    {
        var start = Current.Location;
        var nameTok = ParseNameToken("Expected method name.");

        Expect(TokenType.LParen, "Expected '(' after method name.");
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
        Expect(TokenType.End, "Expected END to close method.");
        if (Check(TokenType.Function))
        {
            Advance(); // optional "END FUNCTION"
        }

        return new MethodDecl(nameTok.Text, parameters, body, visibility, start);
    }

    private ConstructorDecl ParseConstructorDecl(Visibility visibility)
    {
        var start = Current.Location;
        Advance(); // CONSTRUCTOR

        Expect(TokenType.LParen, "Expected '(' after CONSTRUCTOR.");
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

        Expect(TokenType.RParen, "Expected ')' after constructor parameter list.");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close constructor.");
        if (Check(TokenType.Constructor))
        {
            Advance(); // optional "END CONSTRUCTOR"
        }

        return new ConstructorDecl(parameters, body, start);
    }

    private ParameterDecl ParseParameter()
    {
        var nameTok = Expect(TokenType.Identifier, "Expected parameter name.");
        var typeName = TryParseTypeAnnotation();
        return new ParameterDecl(nameTok.Text, typeName, nameTok.Location);
    }

    private UsingDecl ParseUsingDecl()
    {
        var start = Current.Location;
        Advance(); // consume USING

        var parts = new List<string>();
        var first = ParseAnyNameToken("Expected module name after USING.");
        parts.Add(first);

        while (Check(TokenType.Dot))
        {
            Advance(); // consume '.'
            var next = Expect(TokenType.Identifier, "Expected identifier after '.' in USING declaration.").Text;
            parts.Add(next);
        }

        return new UsingDecl(parts, start);
    }

    private EventDecl ParseEventDecl()
    {
        var start = Current.Location;
        Advance(); // consume EVENT
        var nameTok = Expect(TokenType.Identifier, "Expected event name after EVENT.");
        Expect(TokenType.LParen, "Expected '(' after event name.");
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

        Expect(TokenType.RParen, "Expected ')' after event parameter list.");
        return new EventDecl(nameTok.Text, parameters, start);
    }

    private EnumDecl ParseEnumDecl()
    {
        var start = Current.Location;
        Advance(); // consume ENUM
        var nameTok = Expect(TokenType.Identifier, "Expected enum name after ENUM.");
        var members = new List<EnumMember>();
        SkipNewlines();

        while (!IsAtEnd && !Check(TokenType.End))
        {
            if (Check(TokenType.Newline))
            {
                Advance();
                continue;
            }

            var memberStart = Current.Location;
            var memberNameTok = Expect(TokenType.Identifier, "Expected enum member name.");
            Expr? memberValue = null;

            if (Check(TokenType.Equal))
            {
                Advance();
                memberValue = ParseExpression();
            }

            members.Add(new EnumMember(memberNameTok.Text, memberValue, memberStart));
            SkipNewlines();
        }

        Expect(TokenType.End, "Expected END to close ENUM.");
        return new EnumDecl(nameTok.Text, members, start);
    }

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
    private string? TryParseTypeAnnotation()
    {
        if (CheckAny(TokenType.Number, TokenType.String, TokenType.Boolean))
        {
            return Advance().Text;
        }

        return null;
    }

    private string ParseMemberName()
    {
        if (Check(TokenType.Identifier))
        {
            return Advance().Text;
        }

        if (Current.Type is TokenType.Sqrt or TokenType.Ceil or TokenType.Floor or TokenType.Pow or TokenType.Count or TokenType.Str or TokenType.Bool or TokenType.Clear or TokenType.Show or TokenType.Mod or TokenType.TypeOf)
        {
            return Advance().Text;
        }

        throw new SyntaxException(Current.Location, "Expected member name after '.'.");
    }

    private string ParseAnyNameToken(string errorMessage)
    {
        if (Check(TokenType.Identifier))
        {
            return Advance().Text;
        }

        if (Current.Type is TokenType.Sqrt or TokenType.Ceil or TokenType.Floor or TokenType.Pow or TokenType.Count or TokenType.Str or TokenType.Bool or TokenType.Math or TokenType.File or TokenType.Dir or TokenType.Show or TokenType.Mod or TokenType.TypeOf or TokenType.Osl)
        {
            return Advance().Text;
        }

        throw new SyntaxException(Current.Location, errorMessage);
    }

    private Token ParseNameToken(string errorMessage)
    {
        if (Check(TokenType.Identifier))
        {
            return Advance();
        }

        if (Current.Type is TokenType.Sqrt or TokenType.Ceil or TokenType.Floor or TokenType.Pow or TokenType.Count or TokenType.Str or TokenType.Bool or TokenType.Clear or TokenType.Show or TokenType.Mod or TokenType.TypeOf or TokenType.Print or TokenType.Input or TokenType.Using or TokenType.New or TokenType.Me or TokenType.Base or TokenType.Not or TokenType.And or TokenType.Or or TokenType.Return or TokenType.If or TokenType.For or TokenType.While or TokenType.Do or TokenType.Break or TokenType.Continue or TokenType.Elif or TokenType.Else or TokenType.End or TokenType.Then or TokenType.To or TokenType.Step or TokenType.Switch or TokenType.Case or TokenType.Default or TokenType.Try or TokenType.Catch or TokenType.Class or TokenType.Interface or TokenType.Event or TokenType.Var or TokenType.Global or TokenType.Constructor or TokenType.Virtual or TokenType.Override or TokenType.Event or TokenType.On or TokenType.Raise)
        {
            return Advance();
        }

        throw new SyntaxException(Current.Location, errorMessage);
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

        return new IdentifierExpr(tok.Text, tok.Location);
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
