using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
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

        if (Current.Type is TokenType.Sqrt or TokenType.Ceil or TokenType.Floor or TokenType.Pow or TokenType.Count or TokenType.Str or TokenType.Bool or TokenType.Clear or TokenType.Show or TokenType.Mod or TokenType.TypeOf or TokenType.Math or TokenType.File or TokenType.Dir or TokenType.Osl or TokenType.Osb or TokenType.End)
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

        if (Current.Type is TokenType.Sqrt or TokenType.Ceil or TokenType.Floor or TokenType.Pow or TokenType.Count or TokenType.Str or TokenType.Bool or TokenType.Math or TokenType.File or TokenType.Dir or TokenType.Show or TokenType.Mod or TokenType.TypeOf or TokenType.Osl or TokenType.Osb or TokenType.End)
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
}
