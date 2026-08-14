using System.Text;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Lexing;

/// <summary>
/// Lexer (analisador léxico) de OSLANG 0.1.
///
/// Decisões de design não explicitadas na especificação (documentadas aqui em vez
/// de inventadas silenciosamente):
///
/// 1. Strings suportam apenas as sequências de escape \" e \\. Nenhuma outra
///    (\n, \t, etc.) é reconhecida em 0.1 - a especificação não menciona escapes,
///    então o mínimo necessário para permitir aspas dentro de uma string foi
///    adicionado, e nada além disso.
///
/// 2. Números literais são sempre não-negativos (dígitos, opcionalmente um único
///    ponto decimal seguido de dígitos). "-3" é lexado como MINUS seguido de um
///    NumberLiteral "3" - o sinal de menos é unário e resolvido pelo parser, não
///    pelo lexer. Isso é o design convencional e evita ambiguidade com subtração.
///
/// 3. Cada quebra de linha física gera um único token Newline. Linhas em branco
///    (ou comentários sozinhos em uma linha) portanto também geram um Newline; cabe
///    ao parser (não ao lexer) tratar Newlines consecutivos como statements vazios
///    e ignorá-los.
///
/// 4. REM não está na lista de palavras reservadas (seção 5), mas a seção 3 exige
///    suporte a comentários "REM comentário". O lexer reconhece REM apenas quando
///    aparece como uma palavra isolada no início do reconhecimento de um
///    identificador (mesma regra de um identificador comum), e nesse caso consome
///    o restante da linha como comentário em vez de emitir um token Identifier.
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _column = 1;

    public Lexer(string source)
    {
        _source = source;
    }

    /// <summary>Lexa o código-fonte inteiro e retorna a lista de tokens, terminada por um token Eof.</summary>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token token;
        do
        {
            token = NextToken();
            tokens.Add(token);
        } while (token.Type != TokenType.Eof);

        return tokens;
    }

    private bool IsAtEnd => _pos >= _source.Length;

    private char Current => _pos < _source.Length ? _source[_pos] : '\0';

    private char Peek(int offset = 1) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    private SourceLocation CurrentLocation => new(_line, _column);

    private char Advance()
    {
        var c = _source[_pos];
        _pos++;
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return c;
    }

    private Token NextToken()
    {
        SkipWhitespaceAndComments();

        if (IsAtEnd)
        {
            return new Token(TokenType.Eof, string.Empty, CurrentLocation);
        }

        var start = CurrentLocation;
        var c = Current;

        if (c == '\r' || c == '\n')
        {
            return ReadNewline();
        }

        if (char.IsDigit(c))
        {
            return ReadNumber();
        }

        if (c == '"')
        {
            return ReadString();
        }

        if (IsIdentifierStart(c))
        {
            return ReadIdentifierOrKeywordOrRemComment();
        }

        return ReadOperatorOrPunctuation(start);
    }

    /// <summary>
    /// Consome espaços/tabs e comentários (REM ... e ' ...) que não fazem parte de
    /// nenhum token. Não consome quebras de linha - elas são tokens (Newline).
    /// </summary>
    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            var c = Current;

            if (c == ' ' || c == '\t')
            {
                Advance();
                continue;
            }

            if (c == '\'')
            {
                SkipToEndOfLine();
                continue;
            }

            // REM comment: só conta como comentário se "REM" aparecer como palavra
            // isolada (não como prefixo de um identificador maior, ex. "REMOTE").
            if ((c == 'r' || c == 'R') && IsRemKeywordAhead())
            {
                SkipToEndOfLine();
                continue;
            }

            break;
        }
    }

    private bool IsRemKeywordAhead()
    {
        if (Peek(0) is not ('r' or 'R') || Peek(1) is not ('e' or 'E') || Peek(2) is not ('m' or 'M'))
        {
            return false;
        }

        var after = Peek(3);
        return after == '\0' || after == ' ' || after == '\t' || after == '\r' || after == '\n';
    }

    private void SkipToEndOfLine()
    {
        while (!IsAtEnd && Current != '\n' && Current != '\r')
        {
            Advance();
        }
    }

    private Token ReadNewline()
    {
        var start = CurrentLocation;
        var c = Advance();
        if (c == '\r' && Current == '\n')
        {
            Advance();
        }

        return new Token(TokenType.Newline, "\n", start);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private Token ReadIdentifierOrKeywordOrRemComment()
    {
        var start = CurrentLocation;
        var sb = new StringBuilder();

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            sb.Append(Advance());
        }

        var text = sb.ToString();
        var upper = text.ToUpperInvariant();

        return Keywords.TryGetKeyword(upper, out var keywordType)
            ? new Token(keywordType, text, start)
            : new Token(TokenType.Identifier, text, start);
    }

    private Token ReadNumber()
    {
        var start = CurrentLocation;
        var sb = new StringBuilder();

        while (!IsAtEnd && char.IsDigit(Current))
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek()))
        {
            sb.Append(Advance()); // '.'
            while (!IsAtEnd && char.IsDigit(Current))
            {
                sb.Append(Advance());
            }
        }

        var text = sb.ToString();
        var value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        return new Token(TokenType.NumberLiteral, text, start, NumberValue: value);
    }

    private Token ReadString()
    {
        var start = CurrentLocation;
        Advance(); // opening quote

        var sb = new StringBuilder();
        var raw = new StringBuilder("\"");

        while (true)
        {
            if (IsAtEnd || Current == '\n' || Current == '\r')
            {
                throw new LexicalException(start, "Unterminated string literal.");
            }

            var c = Current;

            if (c == '"')
            {
                Advance();
                raw.Append('"');
                break;
            }

            if (c == '\\' && (Peek() == '"' || Peek() == '\\'))
            {
                Advance();
                var escaped = Advance();
                sb.Append(escaped);
                raw.Append('\\').Append(escaped);
                continue;
            }

            sb.Append(Advance());
            raw.Append(c);
        }

        return new Token(TokenType.StringLiteral, raw.ToString(), start, StringValue: sb.ToString());
    }

    private Token ReadOperatorOrPunctuation(SourceLocation start)
    {
        var c = Advance();

        switch (c)
        {
            case '+': return new Token(TokenType.Plus, "+", start);
            case '-': return new Token(TokenType.Minus, "-", start);
            case '*': return new Token(TokenType.Star, "*", start);
            case '/': return new Token(TokenType.Slash, "/", start);
            case '%': return new Token(TokenType.Percent, "%", start);
            case '(': return new Token(TokenType.LParen, "(", start);
            case ')': return new Token(TokenType.RParen, ")", start);
            case '[': return new Token(TokenType.LBracket, "[", start);
            case ']': return new Token(TokenType.RBracket, "]", start);
            case ',': return new Token(TokenType.Comma, ",", start);
            case ':': return new Token(TokenType.Colon, ":", start);
            case '.': return new Token(TokenType.Dot, ".", start);
            case '=':
                if (Current == '>')
                {
                    Advance();
                    return new Token(TokenType.DoubleArrow, "=>", start);
                }

                return new Token(TokenType.Equal, "=", start);
            case '<':
                if (Current == '>')
                {
                    Advance();
                    return new Token(TokenType.NotEqual, "<>", start);
                }

                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenType.LessEqual, "<=", start);
                }

                return new Token(TokenType.Less, "<", start);
            case '>':
                if (Current == '=')
                {
                    Advance();
                    return new Token(TokenType.GreaterEqual, ">=", start);
                }

                return new Token(TokenType.Greater, ">", start);
            default:
                throw new LexicalException(start, $"Unexpected character '{c}'.");
        }
    }
}
