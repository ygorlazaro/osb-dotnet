using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
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
}
