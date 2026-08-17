using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Lexing;

namespace Osb.Lang.Parsing;

public sealed partial class Parser
{
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
            var next = ParseAnyNameToken("Expected identifier after '.' in USING declaration.");
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
}
