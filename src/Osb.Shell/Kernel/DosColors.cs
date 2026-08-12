namespace Osb.Shell.Kernel;

/// <summary>
/// A paleta clássica de 16 cores do modo texto DOS (a mesma usada pelo COLOR do QBasic
/// em COLOR.BAS). Mapeamos para System.ConsoleColor para preservar a mesma numeração
/// que o OSB original usava em OSB.CFG.
/// </summary>
public static class DosColors
{
    public static readonly string[] Names =
    {
        "Preto", "Azul escuro", "Verde", "Ciano", "Vermelho", "Magenta", "Marrom", "Branco",
        "Cinza", "Azul claro", "Verde claro", "Ciano claro", "Vermelho claro", "Magenta claro",
        "Amarelo", "Branco alta intensidade"
    };

    private static readonly ConsoleColor[] Map =
    {
        ConsoleColor.Black, ConsoleColor.DarkBlue, ConsoleColor.DarkGreen, ConsoleColor.DarkCyan,
        ConsoleColor.DarkRed, ConsoleColor.DarkMagenta, ConsoleColor.DarkYellow, ConsoleColor.Gray,
        ConsoleColor.DarkGray, ConsoleColor.Blue, ConsoleColor.Green, ConsoleColor.Cyan,
        ConsoleColor.Red, ConsoleColor.Magenta, ConsoleColor.Yellow, ConsoleColor.White
    };

    public static ConsoleColor ToConsoleColor(int dosColor)
    {
        if (dosColor < 0 || dosColor > 15) dosColor = 7;
        return Map[dosColor];
    }
}
