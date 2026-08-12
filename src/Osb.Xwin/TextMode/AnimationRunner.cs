using Osb.Xwin.Effects;

namespace Osb.Xwin.TextMode;

/// <summary>Roda uma animação de tela em modo texto até o usuário apertar uma tecla.</summary>
public static class AnimationRunner
{
    public static void Run(IScreenEffect effect)
    {
        Console.CursorVisible = false;
        Console.Write("\u001b[2J"); // limpa a tela uma vez, no início

        var charWidth = Math.Max(20, Math.Min(Console.WindowWidth, 120));
        var charHeight = Math.Max(10, Math.Min(Console.WindowHeight - 2, 45));
        var canvas = new TextCanvas(charWidth, charHeight);

        var (designWidth, designHeight) = effect.DesignResolution;
        var scaleX = canvas.PixelWidth / designWidth;
        var scaleY = canvas.PixelHeight / designHeight;
        var scale = Math.Min(scaleX, scaleY);

        try
        {
            while (!Console.KeyAvailable)
            {
                effect.Advance();
                canvas.Clear();
                foreach (var shape in effect.CurrentShapes)
                {
                    var radius = shape.Radius * scale;
                    if (radius < 0)
                    {
                        continue;
                    }

                    canvas.DrawCircle(shape.CenterX * scaleX, shape.CenterY * scaleY, radius, shape.Color);
                }
                foreach (var line in effect.CurrentLines)
                {
                    canvas.DrawLine(line.X1 * scaleX, line.Y1 * scaleY, line.X2 * scaleX, line.Y2 * scaleY, line.Color);
                }
                foreach (var point in effect.CurrentPoints)
                {
                    canvas.SetPixel((int)Math.Round(point.X * scaleX), (int)Math.Round(point.Y * scaleY), point.Color);
                }
                Console.Out.Write(canvas.RenderFrame());
                Console.Out.Write($"{AnsiPalette.Reset}Pressione qualquer tecla para voltar ao menu...");
                Thread.Sleep(30);
            }
            Console.ReadKey(true);
            // Drena qualquer coisa que ainda tenha ficado no buffer (ex: o resto de uma
            // sequência de mouse SGR de vários bytes, se o usuário clicou pra sair em
            // vez de apertar uma tecla) - senão essas sobras vazam pro próximo menu.
            var drainStart = DateTime.UtcNow;
            while (Console.KeyAvailable || (DateTime.UtcNow - drainStart).TotalMilliseconds < 50)
            {
                if (Console.KeyAvailable) { Console.ReadKey(true); drainStart = DateTime.UtcNow; }
                else
                {
                    Thread.Sleep(2);
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.Write("\u001b[2J\u001b[H");
        }
    }
}
