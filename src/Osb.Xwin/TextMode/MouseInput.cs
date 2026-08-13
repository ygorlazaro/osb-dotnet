using System.Diagnostics;
using System.Text;

namespace Osb.Xwin.TextMode;

/// <summary>Um clique ou movimento de mouse, em coordenadas de CARACTERE do terminal (coluna/linha, base 1).</summary>
public readonly record struct MouseClick(int Column, int Row, bool IsPress, bool IsDrag = false);

/// <summary>Um evento de entrada: ou uma tecla normal, ou um clique/movimento de mouse.</summary>
public readonly struct InputEvent
{
    public ConsoleKeyInfo? Key { get; private init; }
    public MouseClick? Mouse { get; private init; }

    public static InputEvent FromKey(ConsoleKeyInfo key) => new() { Key = key };
    public static InputEvent FromMouse(MouseClick mouse) => new() { Mouse = mouse };
}

/// <summary>
/// Suporte a mouse em terminal via protocolo SGR (xterm mouse reporting), o mesmo
/// que terminais modernos (incluindo os do Linux/macOS) entendem. Não depende de
/// X11/Wayland - é só uma sequência ANSI que o próprio emulador de terminal
/// intercepta e reporta de volta como texto na entrada padrão.
/// </summary>
public static class MouseInput
{
    /// <summary>Liga o relato de cliques e arrastes do mouse (modo 1000 = clique, 1002 = movimento com botão pressionado, 1006 = SGR).</summary>
    public static void Enable() => Console.Write("\u001b[?1000h\u001b[?1002h\u001b[?1006h");

    /// <summary>Desliga o relato de mouse - ESSENCIAL chamar ao sair, senão o terminal (inclusive
    /// o prompt do OSB, que herda o mesmo terminal depois que o XWIN termina) fica recebendo
    /// bytes de clique como se fossem texto digitado.</summary>
    public static void Disable() => Console.Write("\u001b[?1006l\u001b[?1002l\u001b[?1000l");

    /// <summary>Lê o próximo evento de entrada, bloqueando até haver uma tecla ou clique/movimento.</summary>
    public static InputEvent Read()
    {
        var key = Console.ReadKey(true);
        if (key.Key != ConsoleKey.Escape)
        {
            return InputEvent.FromKey(key);
        }

        // Pode ser um ESC "de verdade" (usuário quer voltar) ou o início de uma sequência
        // de mouse SGR: ESC [ < Cb ; Cx ; Cy (M ou m). Espera um pouquinho pra ver se vem mais.
        if (!WaitAvailable())
        {
            return InputEvent.FromKey(key);
        }

        var c1 = Console.ReadKey(true);
        if (c1.KeyChar != '[')
        {
            return InputEvent.FromKey(key);
        }

        if (!WaitAvailable())
        {
            return InputEvent.FromKey(key);
        }

        var c2 = Console.ReadKey(true);
        if (c2.KeyChar != '<')
        {
            return InputEvent.FromKey(key);
        }

        var sb = new StringBuilder();
        var terminator = '\0';
        while (WaitAvailable())
        {
            var c = Console.ReadKey(true);
            if (c.KeyChar is 'M' or 'm') { terminator = c.KeyChar; break; }
            sb.Append(c.KeyChar);
        }

        var parts = sb.ToString().Split(';');
        if (terminator != '\0' && parts.Length == 3
            && int.TryParse(parts[0], out var buttonCode)
            && int.TryParse(parts[1], out var cx)
            && int.TryParse(parts[2], out var cy))
        {
            var isDrag = (buttonCode & 32) != 0;
            var isPress = (terminator == 'M');
            // Ajusta coordenadas de 1-based (protocolo SGR) para 0-based usado pelo Console.
            return InputEvent.FromMouse(new MouseClick(cx - 1, cy - 1, isPress, isDrag));
        }

        return InputEvent.FromKey(key);
    }

    private static bool WaitAvailable()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 300)
        {
            if (Console.KeyAvailable)
            {
                return true;
            }

            Thread.Sleep(2);
        }
        return Console.KeyAvailable;
    }
}
