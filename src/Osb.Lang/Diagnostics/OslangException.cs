namespace Osb.Lang.Diagnostics;

/// <summary>
/// As quatro categorias de erro previstas na especificação de OSLANG (seção 44):
/// léxico, sintático, semântico e de execução (runtime).
/// </summary>
public enum OslangErrorCategory
{
    Lexical,
    Syntax,
    Semantic,
    Runtime,
}

/// <summary>
/// Exceção base de todos os erros de OSLANG. Toda exceção de OSLANG carrega
/// categoria, localização (quando disponível) e uma mensagem legível por humanos,
/// conforme exigido pela seção 44 da especificação.
/// </summary>
public class OslangException : Exception
{
    public OslangErrorCategory Category { get; }
    public SourceLocation Location { get; }

    public OslangException(OslangErrorCategory category, SourceLocation location, string message)
        : base(message)
    {
        Category = category;
        Location = location;
    }

    /// <summary>
    /// Formata o erro no estilo sugerido pela seção 44:
    /// "OSLANG ERROR\nLine 12, Column 15\nDivision by zero."
    /// </summary>
    public string ToDisplayString() => $"OSLANG {Category.ToString().ToUpperInvariant()} ERROR{Environment.NewLine}{Location}{Environment.NewLine}{Message}";
}

/// <summary>Erro léxico: caractere inválido, string não terminada, etc.</summary>
public sealed class LexicalException : OslangException
{
    public LexicalException(SourceLocation location, string message)
        : base(OslangErrorCategory.Lexical, location, message)
    {
    }
}

/// <summary>Erro sintático: token inesperado, bloco malformado, etc. (fase de parsing).</summary>
public sealed class SyntaxException : OslangException
{
    public SyntaxException(SourceLocation location, string message)
        : base(OslangErrorCategory.Syntax, location, message)
    {
    }
}

/// <summary>
/// Erro semântico: detectável sem executar o programa, mas que não é um erro de
/// sintaxe puro - por exemplo, a ausência de FUNCTION MAIN() (seção 18/50).
/// </summary>
public sealed class SemanticException : OslangException
{
    public SemanticException(SourceLocation location, string message)
        : base(OslangErrorCategory.Semantic, location, message)
    {
    }
}

/// <summary>
/// Erro de execução (runtime): divisão por zero, conversão inválida, acesso a
/// array fora dos limites, atribuição de tipo inválida, etc. (seção 44). É a
/// única categoria de exceção de OSLANG capturada por TRY/CATCH (seção 35) - a
/// variável ERR recebe <see cref="Exception.Message"/> desta exceção.
/// </summary>
public sealed class OslangRuntimeException : OslangException
{
    public OslangRuntimeException(SourceLocation location, string message)
        : base(OslangErrorCategory.Runtime, location, message)
    {
    }
}
