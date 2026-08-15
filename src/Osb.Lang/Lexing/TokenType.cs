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
    Base,
    Boolean,
    Bool,
    Break,
    Catch,
    Ceil,
    Class,
    Clear,
    Constructor,
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
    Interface,
    Me,
    New,
    Not,
    Null,
    Number,
    Or,
    Pow,
    Print,
    Private,
    Protected,
    Public,
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

    // OSLANG 0.3
    Switch,
    Case,
    Default,
    Virtual,
    Override,
    Event,
    On,
    Raise,
    Using,

    // OSLANG 0.4
    Math,
    File,
    Dir,

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
    Colon,
    Dot,
    DoubleArrow,

    // Estrutura
    Newline,
    Eof,
}
