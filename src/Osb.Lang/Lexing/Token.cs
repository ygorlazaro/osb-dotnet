using Osb.Lang.Diagnostics;

namespace Osb.Lang.Lexing;

/// <summary>
/// Um token léxico de OSLANG.
///
/// <para><see cref="Lexeme"/> preserva o texto original (útil para mensagens de
/// erro e para identificadores, cujo texto "canônico" a linguagem não define -
/// só exige que comparações sejam case-insensitive, seção 2). Já <see cref="Text"/>
/// já vem normalizado para maiúsculas, para simplificar comparações do parser.</para>
///
/// <para><see cref="NumberValue"/> e <see cref="StringValue"/> só são preenchidos
/// para tokens do tipo <see cref="TokenType.NumberLiteral"/> e
/// <see cref="TokenType.StringLiteral"/>, respectivamente.</para>
/// </summary>
public sealed record Token(
    TokenType Type,
    string Lexeme,
    SourceLocation Location,
    double NumberValue = 0,
    string? StringValue = null)
{
    /// <summary>Texto do lexema normalizado para maiúsculas (identificadores e palavras-chave).</summary>
    public string Text => Lexeme.ToUpperInvariant();

    public override string ToString() => $"{Type} '{Lexeme}' @ {Location}";
}
