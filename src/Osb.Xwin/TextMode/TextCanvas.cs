using System.Text;

namespace Osb.Xwin.TextMode;

/// <summary>
/// Um "framebuffer" que vive inteiramente dentro do terminal: cada caractere do console
/// vira 2 pixels (topo/baixo) usando o caractere de meio-bloco superior (▀) com a cor de
/// primeiro plano para o pixel de cima e a cor de fundo para o pixel de baixo. É a mesma
/// técnica usada há décadas em telas de texto (BBS/ANSI art) para simular gráficos sem
/// nenhum modo gráfico de verdade — e sem precisar de X11, Wayland ou qualquer toolkit.
/// </summary>
public sealed class TextCanvas
{
    private const char UpperHalfBlock = '\u2580';

    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public int CharWidth { get; }
    public int CharHeight { get; }

    private readonly int[] _pixels; // índice de cor DOS (0-15), ou -1 = apagado

    public TextCanvas(int charWidth, int charHeight)
    {
        CharWidth = Math.Max(1, charWidth);
        CharHeight = Math.Max(1, charHeight);
        PixelWidth = CharWidth;
        PixelHeight = CharHeight * 2;
        _pixels = new int[PixelWidth * PixelHeight];
        Clear();
    }

    public void Clear() => Array.Fill(_pixels, -1);

    public void SetPixel(int x, int y, int color)
    {
        if (x < 0 || x >= PixelWidth || y < 0 || y >= PixelHeight)
        {
            return;
        }

        _pixels[y * PixelWidth + x] = color;
    }

    /// <summary>Desenha o contorno de um círculo (equivalente a CIRCLE do QBasic), via algoritmo do ponto médio.</summary>
    public void DrawCircle(double cx, double cy, double radius, int color)
    {
        var r = (int)Math.Round(radius);
        var icx = (int)Math.Round(cx);
        var icy = (int)Math.Round(cy);

        if (r <= 0) { SetPixel(icx, icy, color); return; }

        int x = r, y = 0, err = 0;
        while (x >= y)
        {
            PlotOctants(icx, icy, x, y, color);
            y++;
            if (err <= 0)
            {
                err += 2 * y + 1;
            }

            if (err > 0) { x--; err -= 2 * x + 1; }
        }
    }

    private void PlotOctants(int cx, int cy, int x, int y, int color)
    {
        SetPixel(cx + x, cy + y, color);
        SetPixel(cx + y, cy + x, color);
        SetPixel(cx - y, cy + x, color);
        SetPixel(cx - x, cy + y, color);
        SetPixel(cx - x, cy - y, color);
        SetPixel(cx - y, cy - x, color);
        SetPixel(cx + y, cy - x, color);
        SetPixel(cx + x, cy - y, color);
    }

    /// <summary>Desenha uma linha (equivalente a LINE do QBasic), via algoritmo de Bresenham.</summary>
    public void DrawLine(double x1, double y1, double x2, double y2, int color)
    {
        int ix1 = (int)Math.Round(x1), iy1 = (int)Math.Round(y1);
        int ix2 = (int)Math.Round(x2), iy2 = (int)Math.Round(y2);

        int dx = Math.Abs(ix2 - ix1), sx = ix1 < ix2 ? 1 : -1;
        int dy = -Math.Abs(iy2 - iy1), sy = iy1 < iy2 ? 1 : -1;
        var err = dx + dy;

        int x = ix1, y = iy1;
        while (true)
        {
            SetPixel(x, y, color);
            if (x == ix2 && y == iy2)
            {
                break;
            }

            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    /// <summary>Monta o quadro inteiro como uma única string, movendo o cursor para o topo antes de escrever (sem CLS, para não piscar).</summary>
    public string RenderFrame()
    {
        var sb = new StringBuilder(PixelWidth * CharHeight * 12);
        sb.Append("\u001b[H"); // cursor para o canto superior esquerdo

        int lastFg = int.MinValue, lastBg = int.MinValue;
        for (var row = 0; row < CharHeight; row++)
        {
            var topY = row * 2;
            var botY = topY + 1;
            for (var col = 0; col < PixelWidth; col++)
            {
                var top = _pixels[topY * PixelWidth + col];
                var bot = _pixels[botY * PixelWidth + col];

                if (top == -1 && bot == -1)
                {
                    if (lastFg != -100) { sb.Append(AnsiPalette.Reset); lastFg = -100; lastBg = -100; }
                    sb.Append(' ');
                    continue;
                }

                var fg = top == -1 ? 0 : top;
                var bg = bot == -1 ? 0 : bot;
                if (fg != lastFg) { sb.Append(AnsiPalette.Fg(fg)); lastFg = fg; }
                if (bg != lastBg) { sb.Append(AnsiPalette.Bg(bg)); lastBg = bg; }
                sb.Append(UpperHalfBlock);
            }
            sb.Append(AnsiPalette.Reset).Append('\n');
            lastFg = int.MinValue;
            lastBg = int.MinValue;
        }
        return sb.ToString();
    }
}
