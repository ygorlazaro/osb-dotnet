using System.Collections.Generic;
using System.Text;

namespace Osb.Shell.Kernel;

public static class OslangHighlighter
{
    public const string AnsiReset = "\u001b[0m";
    public const string AnsiKeyword = "\u001b[96m";
    public const string AnsiType = "\u001b[93m";
    public const string AnsiString = "\u001b[91m";
    public const string AnsiNumber = "\u001b[95m";
    public const string AnsiComment = "\u001b[90m";

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "BASE", "BOOL", "BREAK", "CATCH", "CEIL", "CLASS", "CLEAR", "CLS", "CONSTRUCTOR",
        "CONTINUE", "COUNT", "DO", "ELIF", "ELSE", "END", "FALSE", "FLOOR", "FOR", "FUNCTION",
        "GLOBAL", "IF", "INPUT", "INTERFACE", "KISS", "ME", "NEW", "NOT", "OR", "POW", "PRINT", "PRIVATE",
        "PROTECTED", "PUBLIC", "RETURN", "SQRT", "STEP", "STR", "THEN", "TO", "TRUE", "TRY", "TYPE", "WHILE",
        "SWITCH", "CASE", "DEFAULT", "VIRTUAL", "OVERRIDE", "EVENT", "ON", "RAISE", "USING",
        "MATH", "FILE", "DIR"
    };

    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "NUMBER", "STRING", "BOOLEAN", "ARRAY", "NULL", "OBJECT"
    };

    public static string Highlight(string line, int maxVisibleWidth)
    {
        if (line.Length == 0) return new string(' ', maxVisibleWidth);

        var segments = Tokenize(line);
        var result = new StringBuilder();
        var visibleLength = 0;
        string? currentColor = null;

        foreach (var (text, color) in segments)
        {
            if (visibleLength >= maxVisibleWidth) break;

            if (!string.Equals(color, currentColor, StringComparison.Ordinal))
            {
                if (currentColor != null)
                {
                    result.Append(AnsiReset);
                }
                result.Append(color);
                currentColor = color;
            }

            var available = maxVisibleWidth - visibleLength;
            if (text.Length > available)
            {
                result.Append(text[..available]);
                visibleLength += available;
            }
            else
            {
                result.Append(text);
                visibleLength += text.Length;
            }
        }

        if (currentColor != null)
        {
            result.Append(AnsiReset);
        }

        if (visibleLength < maxVisibleWidth)
        {
            result.Append(new string(' ', maxVisibleWidth - visibleLength));
        }

        return result.ToString();
    }

    public static IEnumerable<(string Text, string Color)> Tokenize(string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '\'' || (i + 3 <= line.Length && line.Substring(i, 3).Equals("REM", StringComparison.OrdinalIgnoreCase)))
            {
                yield return (line[i..], AnsiComment);
                yield break;
            }

            if (line[i] == '"')
            {
                var start = i;
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    i++;
                }
                if (i < line.Length) i++;
                yield return (line[start..i], AnsiString);
                continue;
            }

            if (char.IsDigit(line[i]) || (line[i] == '-' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                var start = i;
                i++;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.'))
                {
                    i++;
                }
                yield return (line[start..i], AnsiNumber);
                continue;
            }

            if (char.IsLetter(line[i]) || line[i] == '_')
            {
                var start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                {
                    i++;
                }
                var word = line[start..i];

                string color = AnsiReset;
                if (Keywords.Contains(word))
                {
                    color = AnsiKeyword;
                }
                else if (Types.Contains(word))
                {
                    color = AnsiType;
                }

                yield return (word, color);
                continue;
            }

            yield return (line[i..(i + 1)], AnsiReset);
            i++;
        }
    }
}
