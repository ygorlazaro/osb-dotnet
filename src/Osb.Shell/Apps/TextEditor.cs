using Osb.Shell.Kernel;

namespace Osb.Shell.Apps;

/// <summary>
/// Porte do KISS (o editor de texto simples do OSB original). O .BAS original não
/// sobreviveu em texto, então este é um editor novo, mas com a mesma proposta:
/// simples, direto, em modo texto, sem depender de nada além da Console API.
///
/// Teclas: setas para navegar, Enter quebra linha, Backspace/Delete apagam,
/// Ctrl+S salva, Ctrl+Q ou ESC sai (perguntando antes se há alterações não salvas).
/// </summary>
public static class TextEditor
{
    public static void Run(string filenameArg, OsbEnvironment env)
    {
        var filename = filenameArg.Trim();
        var path = filename == "" ? null : PathResolver.Resolve(filename);

        List<string> lines;
        try
        {
            lines = path is not null && File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string> { "" };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao abrir o arquivo: " + ex.Message);
            return;
        }
        if (lines.Count == 0) lines.Add("");

        int row = 0, col = 0, scrollTop = 0;
        bool modified = false;
        bool running = true;
        string? status = null;

        var prevCursorVisible = true;
        try { prevCursorVisible = Console.CursorVisible; } catch { /* alguns terminais não suportam ler isso */ }
        Console.CursorVisible = true;
        env.ApplyColors(); // o KISS usa o mesmo esquema de cor configurado no OSB (COLOR)
        Console.Clear();

        while (running)
        {
            var visibleRows = Math.Max(3, Console.WindowHeight - 2);
            if (row < scrollTop) scrollTop = row;
            if (row >= scrollTop + visibleRows) scrollTop = row - visibleRows + 1;

            Render(lines, row, col, scrollTop, visibleRows, filename, modified, status);
            status = null;

            var key = Console.ReadKey(true);

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.S)
            {
                status = Save(lines, ref filename, ref path);
                modified = false;
                continue;
            }

            if (key.Key == ConsoleKey.Escape || (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.Q))
            {
                if (modified && !ConfirmDiscard()) continue;
                running = false;
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (row > 0) row--;
                    col = Math.Min(col, lines[row].Length);
                    break;
                case ConsoleKey.DownArrow:
                    if (row < lines.Count - 1) row++;
                    col = Math.Min(col, lines[row].Length);
                    break;
                case ConsoleKey.LeftArrow:
                    if (col > 0) col--;
                    else if (row > 0) { row--; col = lines[row].Length; }
                    break;
                case ConsoleKey.RightArrow:
                    if (col < lines[row].Length) col++;
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

        Console.Clear();
        Console.CursorVisible = prevCursorVisible;
    }

    private static string Save(List<string> lines, ref string filename, ref string? path)
    {
        if (path is null)
        {
            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            Console.Write("Salvar como: ");
            var typed = (Console.ReadLine() ?? "").Trim();
            if (typed == "") return "Salvamento cancelado (nenhum nome informado).";
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

        for (int i = 0; i < visibleRows; i++)
        {
            var lineIndex = scrollTop + i;
            var text = lineIndex < lines.Count ? lines[lineIndex] : "~";
            if (text.Length > width) text = text[..width];
            Console.Write(text.PadRight(width));
            if (i < visibleRows - 1) Console.WriteLine();
        }

        Console.WriteLine();
        var title = filename == "" ? "(sem nome)" : filename;
        var header = $" KISS - {title}{(modified ? " [modificado]" : "")} ";
        if (header.Length > width) header = header[..width];
        Console.Write(header.PadRight(width));

        var help = status ?? "Ctrl+S salva | ESC/Ctrl+Q sai";
        if (help.Length > width) help = help[..width];
        Console.Write(help.PadRight(width));

        var screenRow = row - scrollTop;
        Console.SetCursorPosition(Math.Min(col, width - 1), Math.Clamp(screenRow, 0, visibleRows - 1));
    }
}
