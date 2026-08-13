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
public sealed class LexicalException(SourceLocation location, string message)
    : OslangException(OslangErrorCategory.Lexical, location, message);
