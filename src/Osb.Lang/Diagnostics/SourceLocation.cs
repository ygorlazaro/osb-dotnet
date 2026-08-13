namespace Osb.Lang.Diagnostics;

/// <summary>
/// Posição de um token/erro no código-fonte OSLANG. Linha e coluna são 1-based,
/// que é o formato mais legível para mensagens de erro ("Line 12, Column 15").
/// </summary>
public readonly record struct SourceLocation(int Line, int Column)
{
    public static readonly SourceLocation Unknown = new(0, 0);

    public override string ToString() => $"Line {Line}, Column {Column}";
}
