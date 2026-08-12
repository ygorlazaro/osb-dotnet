namespace Osb.Xwin.TextMode;

/// <summary>
/// A mesma paleta de 16 cores do modo VGA (SCREEN 12) que RADIAIS.BAS e CICULOS.BAS
/// usavam, aqui mapeada para sequências ANSI de cor verdadeira (24 bits), que qualquer
/// terminal moderno entende — sem precisar de X11, Wayland, ou qualquer servidor gráfico.
/// </summary>
public static class AnsiPalette
{
    private static readonly (byte R, byte G, byte B)[] Colors =
    {
        (0, 0, 0),       // 0 Preto
        (0, 0, 170),     // 1 Azul escuro
        (0, 170, 0),     // 2 Verde
        (0, 170, 170),   // 3 Ciano
        (170, 0, 0),     // 4 Vermelho
        (170, 0, 170),   // 5 Magenta
        (170, 85, 0),    // 6 Marrom
        (170, 170, 170), // 7 Branco
        (85, 85, 85),    // 8 Cinza
        (85, 85, 255),   // 9 Azul claro
        (85, 255, 85),   // 10 Verde claro
        (85, 255, 255),  // 11 Ciano claro
        (255, 85, 85),   // 12 Vermelho claro
        (255, 85, 255),  // 13 Magenta claro
        (255, 255, 85),  // 14 Amarelo
        (255, 255, 255), // 15 Branco alta intensidade
    };

    public const int Transparent = -1;

    private static (byte R, byte G, byte B) Get(int index)
    {
        var i = ((index % 16) + 16) % 16;
        return Colors[i];
    }

    /// <summary>Sequência ANSI para definir a cor de primeiro plano (foreground).</summary>
    public static string Fg(int index)
    {
        var (r, g, b) = Get(index);
        return $"\u001b[38;2;{r};{g};{b}m";
    }

    /// <summary>Sequência ANSI para definir a cor de fundo (background).</summary>
    public static string Bg(int index)
    {
        var (r, g, b) = Get(index);
        return $"\u001b[48;2;{r};{g};{b}m";
    }

    public const string Reset = "\u001b[0m";
}
