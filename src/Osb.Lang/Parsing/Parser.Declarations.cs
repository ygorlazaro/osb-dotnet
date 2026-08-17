using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
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
}
