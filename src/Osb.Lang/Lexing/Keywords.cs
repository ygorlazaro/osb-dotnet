namespace Osb.Lang.Lexing;

/// <summary>
/// Palavras reservadas de OSLANG 0.1 (seção 5 da especificação), mapeadas do texto
/// em maiúsculas para o TokenType correspondente. Usada pelo lexer para decidir se
/// um identificador lido é, na verdade, uma palavra-chave.
///
/// Observação de design: ABS não está na lista de palavras reservadas da seção 5,
/// mesmo aparecendo entre as funções matemáticas da seção 41 - por isso ABS é
/// tratado como um identificador comum (chamada de função de biblioteca padrão),
/// e não como uma palavra-chave da linguagem. Isso segue a especificação ao pé da
/// letra em vez de "corrigir" a aparente inconsistência.
/// </summary>
public static class Keywords
{
    public static readonly IReadOnlyDictionary<string, TokenType> Map = new Dictionary<string, TokenType>
    {
        ["AND"] = TokenType.And,
        ["BASE"] = TokenType.Base,
        ["BOOLEAN"] = TokenType.Boolean,
        ["BOOL"] = TokenType.Bool,
        ["BREAK"] = TokenType.Break,
        ["CATCH"] = TokenType.Catch,
        ["CEIL"] = TokenType.Ceil,
        ["CLASS"] = TokenType.Class,
        ["CLEAR"] = TokenType.Clear,
        ["CONSTRUCTOR"] = TokenType.Constructor,
        ["CONTINUE"] = TokenType.Continue,
        ["COUNT"] = TokenType.Count,
        ["DO"] = TokenType.Do,
        ["ELIF"] = TokenType.Elif,
        ["ELSE"] = TokenType.Else,
        ["END"] = TokenType.End,
        ["FALSE"] = TokenType.False,
        ["FLOOR"] = TokenType.Floor,
        ["FOR"] = TokenType.For,
        ["FUNCTION"] = TokenType.Function,
        ["GLOBAL"] = TokenType.Global,
        ["IF"] = TokenType.If,
        ["INPUT"] = TokenType.Input,
        ["INTERFACE"] = TokenType.Interface,
        ["ME"] = TokenType.Me,
        ["NEW"] = TokenType.New,
        ["NOT"] = TokenType.Not,
        ["NULL"] = TokenType.Null,
        ["NUMBER"] = TokenType.Number,
        ["OR"] = TokenType.Or,
        ["POW"] = TokenType.Pow,
        ["PRINT"] = TokenType.Print,
        ["PRIVATE"] = TokenType.Private,
        ["PROTECTED"] = TokenType.Protected,
        ["PUBLIC"] = TokenType.Public,
        ["RETURN"] = TokenType.Return,
        ["SQRT"] = TokenType.Sqrt,
        ["STEP"] = TokenType.Step,
        ["STRING"] = TokenType.String,
        ["STR"] = TokenType.Str,
        ["THEN"] = TokenType.Then,
        ["TO"] = TokenType.To,
        ["TRUE"] = TokenType.True,
        ["TRY"] = TokenType.Try,
        ["TYPEOF"] = TokenType.TypeOf,
        ["VAR"] = TokenType.Var,
        ["WHILE"] = TokenType.While,
        ["SWITCH"] = TokenType.Switch,
        ["CASE"] = TokenType.Case,
        ["DEFAULT"] = TokenType.Default,
        ["VIRTUAL"] = TokenType.Virtual,
        ["OVERRIDE"] = TokenType.Override,
        ["EVENT"] = TokenType.Event,
        ["ON"] = TokenType.On,
        ["RAISE"] = TokenType.Raise,
        ["USING"] = TokenType.Using,
        ["MATH"] = TokenType.Math,
        ["FILE"] = TokenType.File,
        ["DIR"] = TokenType.Dir,
        ["SHOW"] = TokenType.Show,
        ["MOD"] = TokenType.Mod,
        ["OSL"] = TokenType.Osl,
        ["ENUM"] = TokenType.Enum,
    };

    public static bool TryGetKeyword(string upperText, out TokenType type) => Map.TryGetValue(upperText, out type);
}
