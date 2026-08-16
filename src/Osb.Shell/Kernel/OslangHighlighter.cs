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
    public const string AnsiOperator = "\u001b[97m";
    public const string AnsiMethod = "\u001b[94m";
    public const string AnsiPunctuation = "\u001b[37m";

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "BASE", "BOOL", "BOOLEAN", "BREAK", "CATCH", "CEIL", "CLASS", "CLEAR", "CLS", "CONSTRUCTOR",
        "CONTINUE", "COUNT", "DO", "ELIF", "ELSE", "END", "FALSE", "FLOOR", "FOR", "FUNCTION",
        "GLOBAL", "IF", "INPUT", "INTERFACE", "KISS", "ME", "NEW", "NOT", "OR", "POW", "PRINT", "PRIVATE",
        "PROTECTED", "PUBLIC", "RETURN", "SQRT", "STEP", "STR", "STRING", "THEN", "TO", "TRUE", "TRY", "TYPE", "WHILE",
        "SWITCH", "CASE", "DEFAULT", "VIRTUAL", "OVERRIDE", "EVENT", "ON", "RAISE", "USING",
        "MATH", "FILE", "DIR", "DATE", "TIME", "SHOW", "MOD", "TYPEOF", "ENUM", "OSL", "OSB",
        "JSON", "CSV", "XML", "CNF", "NET"
    };

    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "NUMBER", "STRING", "BOOLEAN", "ARRAY", "NULL", "OBJECT", "DATE", "TIME"
    };

    private static readonly HashSet<string> BuiltinFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "STR", "NUMBER", "BOOL", "SQRT", "ABS", "POW", "FLOOR", "CEIL", "COUNT", "TYPEOF",
        "LENGTH", "TOUPPER", "TOLOWER", "TRIM", "SUBSTR", "CONTAINS", "REVERSE", "NORMALIZE",
        "SORT", "JOIN", "FIRST", "LAST", "INDEXOF", "REMOVE", "FLAT", "PUSH", "POP",
        "FINDINDEX", "REPEAT", "PADSTART", "PADEND", "TRUNC", "SIN", "COS", "TAN", "PI",
        "RANDOM", "NOW", "FORMAT", "EXISTS", "LIST", "FILES", "DIRS", "CREATE", "DELETE",
        "CURRENT", "NAME", "VALUE", "NAME", "KEYS", "GET", "HAS", "SETLANGUAGE", "LANGUAGES",
        "LANGUAGE", "LOAD", "LOADLANGUAGE", "RELOAD", "UNLOAD", "DEFAULT", "SETDEFAULT", "SETFALLBACK"
    };

    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        "**", "++", "--", "+=", "=>", "%", "*", "/", "+", "-", "=", "<>", "<", ">", "<=", ">=", "MOD"
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
                    if (line[i] == '\\' && i + 1 < line.Length)
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                if (i < line.Length) i++;
                yield return (line[start..i], AnsiString);
                continue;
            }

            if (char.IsDigit(line[i]) || (line[i] == '-' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                var start = i;
                if (line[i] == '-') i++;
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
                else if (i < line.Length && line[i] == '(')
                {
                    color = AnsiMethod;
                }

                yield return (word, color);
                continue;
            }

            if (line[i] == '(' || line[i] == ')' || line[i] == '[' || line[i] == ']' || line[i] == '{' || line[i] == '}' || line[i] == ',' || line[i] == ':')
            {
                yield return (line[i..(i + 1)], AnsiPunctuation);
                i++;
                continue;
            }

            if (line[i] == '.' && i + 1 < line.Length && (char.IsLetter(line[i + 1]) || line[i + 1] == '_'))
            {
                yield return (line[i..(i + 1)], AnsiPunctuation);
                i++;
                continue;
            }

            if (i + 1 < line.Length)
            {
                var maxLen = Math.Min(3, line.Length - i);
                var opLen = maxLen;
                while (opLen > 0 && !Operators.Contains(line.Substring(i, opLen)))
                {
                    opLen--;
                }
                if (opLen > 0)
                {
                    yield return (line[i..(i + opLen)], AnsiOperator);
                    i += opLen;
                    continue;
                }
            }

            yield return (line[i..(i + 1)], AnsiReset);
            i++;
        }
    }

    public static string GetFileType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".osl" => "OSLANG",
            ".oslang" => "OSLANG",
            ".cfg" => "CONFIG",
            ".i18n" => "I18N",
            ".hlp" => "HELP",
            ".wds" => "WORDS",
            ".txt" => "TEXT",
            ".md" => "MARKDOWN",
            ".json" => "JSON",
            ".cs" => "C#",
            _ => "TEXT"
        };
    }
}
