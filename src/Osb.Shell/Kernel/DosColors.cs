namespace Osb.Shell.Kernel;

/// <summary>
/// Classic 16-color palette mapped to System.ConsoleColor.
/// </summary>
public static class DosColors
{
    public static readonly string[] Names =
    [
        "Preto", "Azul escuro", "Verde", "Ciano", "Vermelho", "Magenta", "Marrom", "Branco",
        "Cinza", "Azul claro", "Verde claro", "Ciano claro", "Vermelho claro", "Magenta claro",
        "Amarelo", "Branco alta intensidade"
    ];

    private static readonly ConsoleColor[] Map =
    [
        ConsoleColor.Black, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen, ConsoleColor.DarkCyan,
        ConsoleColor.DarkRed, ConsoleColor.DarkMagenta, ConsoleColor.DarkYellow, ConsoleColor.Gray,
        ConsoleColor.DarkGray, ConsoleColor.Blue, ConsoleColor.Green, ConsoleColor.Cyan,
        ConsoleColor.Red, ConsoleColor.Magenta, ConsoleColor.Yellow, ConsoleColor.White
    ];

    public static ConsoleColor ToConsoleColor(int dosColor)
    {
        if (dosColor < 0 || dosColor > 15)
        {
            dosColor = 7;
        }

        return Map[dosColor];
    }
}
