using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Osb.Shell.Kernel;

namespace Osb.Shell.Apps;

public static class TextEditor
{
    private readonly record struct MouseEvent(int Col, int Row, int Button, bool IsPress, bool IsScrollUp, bool IsScrollDown);

    public static void Run(string filenameArg, OsbEnvironment env)
    {
        var filename = filenameArg.Trim();
        var path = filename == "" ? null : PathResolver.Resolve(filename);

        List<string> lines;
        try
        {
            lines = path is not null && File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : [""];
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao abrir o arquivo: " + ex.Message);
            return;
        }
        if (lines.Count == 0)
        {
            lines.Add("");
        }

        int row = 0, col = 0, scrollTop = 0;
        var modified = false;
        var running = true;
        string? status = null;

        var prevCursorVisible = true;
        try { prevCursorVisible = Console.CursorVisible; } catch { /* alguns terminais não suportam ler isso */ }
        Console.CursorVisible = true;
        env.ApplyColors();
        Console.Clear();

        // Ativa suporte a mouse via protocolo ANSI/SGR (1000 = clique, 1002 = movimento/arraste, 1006 = SGR)
        Console.Write("\u001b[?1000h\u001b[?1002h\u001b[?1006h");

        try
        {
            while (running)
            {
                var visibleRows = Math.Max(3, Console.WindowHeight - 2);
                if (row < scrollTop)
                {
                    scrollTop = row;
                }

                if (row >= scrollTop + visibleRows)
                {
                    scrollTop = row - visibleRows + 1;
                }

                Render(lines, row, col, scrollTop, visibleRows, filename, modified, status);
                status = null;

                var key = Console.ReadKey(true);

                // Processa clique ou scroll do mouse se o código for ESC (início de sequência ANSI)
                if (key.Key == ConsoleKey.Escape)
                {
                    var mouse = TryReadMouse();
                    if (mouse is { } m)
                    {
                        if (m.IsScrollUp)
                        {
                            scrollTop = Math.Max(0, scrollTop - 3);
                            row = Math.Max(0, row - 3);
                            col = Math.Min(col, lines[row].Length);
                            continue;
                        }

                        if (m.IsScrollDown)
                        {
                            scrollTop = Math.Min(lines.Count - 1, scrollTop + 3);
                            row = Math.Min(lines.Count - 1, row + 3);
                            col = Math.Min(col, lines[row].Length);
                            continue;
                        }

                        if (m.IsPress || (m.Button & 32) != 0) // clique ou arraste do mouse
                        {
                            if (m.Row >= 0 && m.Row < visibleRows)
                            {
                                row = Math.Clamp(scrollTop + m.Row, 0, lines.Count - 1);
                                col = Math.Clamp(m.Col, 0, lines[row].Length);
                                continue;
                            }

                            if (m.Row == visibleRows + 1) // Clique na barra de ações no rodapé
                            {
                                if (m.Col < 28) // Clique em [Salvar]
                                {
                                    status = Save(lines, ref filename, ref path);
                                    modified = false;
                                }
                                else // Clique em [Sair]
                                {
                                    if (!modified || ConfirmDiscard())
                                    {
                                        running = false;
                                    }
                                }
                                continue;
                            }
                        }

                        continue;
                    }

                    // Se não for sequência de mouse, trata como ESC (Sair)
                    if (!modified || ConfirmDiscard())
                    {
                        running = false;
                    }
                    continue;
                }

                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.S)
                {
                    status = Save(lines, ref filename, ref path);
                    modified = false;
                    continue;
                }

                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.Q)
                {
                    if (modified && !ConfirmDiscard())
                    {
                        continue;
                    }

                    running = false;
                    continue;
                }

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        if (row > 0)
                        {
                            row--;
                        }

                        col = Math.Min(col, lines[row].Length);
                        break;
                    case ConsoleKey.DownArrow:
                        if (row < lines.Count - 1)
                        {
                            row++;
                        }

                        col = Math.Min(col, lines[row].Length);
                        break;
                    case ConsoleKey.LeftArrow:
                        if (col > 0)
                        {
                            col--;
                        }
                        else if (row > 0) { row--; col = lines[row].Length; }
                        break;
                    case ConsoleKey.RightArrow:
                        if (col < lines[row].Length)
                        {
                            col++;
                        }
                        else if (row < lines.Count - 1) { row++; col = 0; }
                        break;
                    case ConsoleKey.Home:
                        col = 0;
                        break;
                    case ConsoleKey.End:
                        col = lines[row].Length;
                        break;
                    case ConsoleKey.PageUp:
                        row = Math.Max(0, row - visibleRows);
                        col = Math.Min(col, lines[row].Length);
                        break;
                    case ConsoleKey.PageDown:
                        row = Math.Min(lines.Count - 1, row + visibleRows);
                        col = Math.Min(col, lines[row].Length);
                        break;
                    case ConsoleKey.Enter:
                        lines.Insert(row + 1, lines[row][col..]);
                        lines[row] = lines[row][..col];
                        row++; col = 0; modified = true;
                        break;
                    case ConsoleKey.Backspace:
                        if (col > 0) { lines[row] = lines[row].Remove(col - 1, 1); col--; modified = true; }
                        else if (row > 0)
                        {
                            col = lines[row - 1].Length;
                            lines[row - 1] += lines[row];
                            lines.RemoveAt(row);
                            row--; modified = true;
                        }
                        break;
                    case ConsoleKey.Delete:
                        if (col < lines[row].Length) { lines[row] = lines[row].Remove(col, 1); modified = true; }
                        else if (row < lines.Count - 1)
                        {
                            lines[row] += lines[row + 1];
                            lines.RemoveAt(row + 1);
                            modified = true;
                        }
                        break;
                    case ConsoleKey.Tab:
                        lines[row] = lines[row].Insert(col, "    ");
                        col += 4; modified = true;
                        break;
                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            lines[row] = lines[row].Insert(col, key.KeyChar.ToString());
                            col++; modified = true;
                        }
                        break;
                }
            }
        }
        finally
        {
            // Desativa o rastreamento do mouse ao sair
            Console.Write("\u001b[?1006l\u001b[?1002l\u001b[?1000l");
            Console.Clear();
            Console.CursorVisible = prevCursorVisible;
        }
    }

    private static MouseEvent? TryReadMouse()
    {
        var sw = Stopwatch.StartNew();
        var hasChar = false;
        while (sw.ElapsedMilliseconds < 35)
        {
            if (Console.KeyAvailable)
            {
                hasChar = true;
                break;
            }
            Thread.Sleep(2);
        }

        if (!hasChar) return null;

        var c1 = Console.ReadKey(true);
        if (c1.KeyChar != '[') return null;

        sw.Restart();
        hasChar = false;
        while (sw.ElapsedMilliseconds < 35)
        {
            if (Console.KeyAvailable)
            {
                hasChar = true;
                break;
            }
            Thread.Sleep(2);
        }

        if (!hasChar) return null;

        var c2 = Console.ReadKey(true);
        if (c2.KeyChar != '<') return null;

        var sb = new StringBuilder();
        var terminator = '\0';

        sw.Restart();
        while (sw.ElapsedMilliseconds < 50)
        {
            if (Console.KeyAvailable)
            {
                var c = Console.ReadKey(true);
                if (c.KeyChar is 'M' or 'm')
                {
                    terminator = c.KeyChar;
                    break;
                }
                sb.Append(c.KeyChar);
            }
            else
            {
                Thread.Sleep(2);
            }
        }

        var parts = sb.ToString().Split(';');
        if (terminator != '\0' && parts.Length == 3 &&
            int.TryParse(parts[0], out var btn) &&
            int.TryParse(parts[1], out var cx) &&
            int.TryParse(parts[2], out var cy))
        {
            var isPress = (terminator == 'M');
            var isScrollUp = (btn == 64);
            var isScrollDown = (btn == 65);
            return new MouseEvent(cx - 1, cy - 1, btn, isPress, isScrollUp, isScrollDown);
        }

        return null;
    }

    private static string Save(List<string> lines, ref string filename, ref string? path)
    {
        if (path is null)
        {
            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.Write("Salvar como: ");
            var typed = (Console.ReadLine() ?? "").Trim();
            if (typed == "")
            {
                return "Salvamento cancelado (nenhum nome informado).";
            }

            filename = typed;
            path = PathResolver.Resolve(typed);
        }

        try
        {
            File.WriteAllLines(path, lines);
            return $"Salvo em {path}";
        }
        catch (Exception ex)
        {
            return "Erro ao salvar: " + ex.Message;
        }
    }

    private static bool ConfirmDiscard()
    {
        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.Write("Há alterações não salvas. Sair mesmo assim? (S/N) ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        return answer == "S";
    }

    private static void Render(List<string> lines, int row, int col, int scrollTop, int visibleRows,
        string filename, bool modified, string? status)
    {
        Console.SetCursorPosition(0, 0);
        var width = Math.Max(20, Console.WindowWidth);
        var lineNumberWidth = Math.Max(4, lines.Count.ToString().Length + 1);
        var textWidth = width - lineNumberWidth;

        for (var i = 0; i < visibleRows; i++)
        {
            var lineIndex = scrollTop + i;
            var text = lineIndex < lines.Count ? lines[lineIndex] : "~";
            if (text.Length > textWidth)
            {
                text = text[..textWidth];
            }

            var lineNumber = lineIndex < lines.Count ? (lineIndex + 1).ToString() : "~";
            Console.Write(lineNumber.PadLeft(lineNumberWidth - 1) + " " + text.PadRight(textWidth));
            if (i < visibleRows - 1)
            {
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        var title = filename == "" ? "(sem nome)" : filename;
        var header = $" KISS - {title}{(modified ? " [modificado]" : "")} ";
        if (header.Length > width)
        {
            header = header[..width];
        }

        Console.Write(header.PadRight(width));

        var help = status ?? "[ Ctrl+S / Clique [Salvar] ]   [ ESC / Clique [Sair] ]";
        if (help.Length > width)
        {
            help = help[..width];
        }

        Console.Write(help.PadRight(width));

        var screenRow = row - scrollTop;
        Console.SetCursorPosition(lineNumberWidth + Math.Min(col, textWidth - 1), Math.Clamp(screenRow, 0, visibleRows - 1));
    }
}
