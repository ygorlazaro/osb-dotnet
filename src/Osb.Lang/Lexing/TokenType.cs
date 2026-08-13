namespace Osb.Lang.Lexing;

public enum TokenType
{
    // Literais e identificadores
    Identifier,
    NumberLiteral,
    StringLiteral,

    // Palavras reservadas (seção 5 da especificação) - uma entrada por palavra-chave,
    // já normalizada para maiúsculas pelo lexer (OSLANG é case-insensitive, seção 2).
    And,
    Boolean,
    Bool,
    Break,
    Catch,
    Ceil,
    Clear,
    Continue,
    Count,
    Do,
    Elif,
    Else,
    End,
    False,
    Floor,
    For,
    Function,
    Global,
    If,
    Input,
    Not,
    Null,
    Number,
    Or,
    Pow,
    Print,
    Return,
    Sqrt,
    Step,
    String,
    Str,
    Then,
    To,
    True,
    Try,
    TypeOf,
    Var,
    While,

    // Operadores aritméticos
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // Operadores de comparação
    Equal,
    NotEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,

    // Pontuação
    LParen,
    RParen,
    LBracket,
    RBracket,
    Comma,

    // Estrutura
    Newline,
    Eof,
}
