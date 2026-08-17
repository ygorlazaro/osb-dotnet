using System.Runtime.InteropServices;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;

namespace Osb.Shell.Kernel;

public sealed class ConsoleHost
{
    private readonly ExtensionRegistry _extensions;
    private bool _inAlternateScreen;
    private bool _cursorVisible = true;
    private bool _inRawMode;
    private (int Row, int Column) _savedCursor;
    private bool _frameOpen;
    private bool _terminalResized;
    private int _width;
    private int _height;

    public ConsoleHost(ExtensionRegistry extensions)
    {
        _extensions = extensions;
        _width = Console.WindowWidth;
        _height = Console.WindowHeight;
    }

    public OslangValue Dispatch(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "WIDTH":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(Console.WindowWidth);
            case "HEIGHT":
                EnsureArgCount(args, 0, methodName, location);
                return new NumberValue(Console.WindowHeight);
            case "SIZE":
                EnsureArgCount(args, 0, methodName, location);
                return new SizeValue(Console.WindowWidth, Console.WindowHeight);
            case "RESIZED":
                EnsureArgCount(args, 0, methodName, location);
                var resized = _terminalResized || Console.WindowWidth != _width || Console.WindowHeight != _height;
                if (resized)
                {
                    _width = Console.WindowWidth;
                    _height = Console.WindowHeight;
                    _terminalResized = false;
                }
                return BooleanValue.Of(resized);
            case "SETCURSOR":
                EnsureArgCount(args, 2, methodName, location);
                var row = (int)RequireNumberArg(args, 0, methodName, location);
                var col = (int)RequireNumberArg(args, 1, methodName, location);
                Console.SetCursorPosition(col - 1, row - 1);
                return OslangValue.Null;
            case "GETCURSOR":
                EnsureArgCount(args, 0, methodName, location);
                var cursor = Console.GetCursorPosition();
                return new CursorPositionValue(cursor.Top + 1, cursor.Left + 1);
            case "HIDECURSOR":
                EnsureArgCount(args, 0, methodName, location);
                if (_cursorVisible)
                {
                    Console.CursorVisible = false;
                    _cursorVisible = false;
                }
                return OslangValue.Null;
            case "SHOWCURSOR":
                EnsureArgCount(args, 0, methodName, location);
                if (!_cursorVisible)
                {
                    Console.CursorVisible = true;
                    _cursorVisible = true;
                }
                return OslangValue.Null;
            case "CLEAR":
                EnsureArgCount(args, 0, methodName, location);
                Console.Write("\x1b[2J");
                Console.SetCursorPosition(0, 0);
                return OslangValue.Null;
            case "CLEARLINE":
                if (args.Count == 0)
                {
                    var current = Console.GetCursorPosition();
                    Console.Write($"\x1b[{current.Top + 1};1H\x1b[2K");
                }
                else if (args.Count == 1)
                {
                    var line = (int)RequireNumberArg(args, 0, methodName, location);
                    Console.Write($"\x1b[{line};1H\x1b[2K");
                }
                else
                {
                    throw new OslangRuntimeException(location, $"{methodName}() expects 0 or 1 arguments, got {args.Count}.");
                }
                return OslangValue.Null;
            case "CLEARAREA":
                EnsureArgCount(args, 4, methodName, location);
                var top = (int)RequireNumberArg(args, 0, methodName, location);
                var left = (int)RequireNumberArg(args, 1, methodName, location);
                var bottom = (int)RequireNumberArg(args, 2, methodName, location);
                var right = (int)RequireNumberArg(args, 3, methodName, location);
                for (var r = top; r <= bottom; r++)
                {
                    Console.Write($"\x1b[{r};{left}H\x1b[{right - left + 1}X");
                }
                return OslangValue.Null;
            case "WRITE":
                EnsureArgCount(args, 3, methodName, location);
                var wRow = (int)RequireNumberArg(args, 0, methodName, location);
                var wCol = (int)RequireNumberArg(args, 1, methodName, location);
                var wText = RequireStringArg(args, 2, methodName, location);
                Console.Write($"\x1b[{wRow};{wCol}H{wText}");
                return OslangValue.Null;
            case "GETKEY":
                EnsureArgCount(args, 0, methodName, location);
                return ReadKeyBlocking(location);
            case "READKEY":
                EnsureArgCount(args, 0, methodName, location);
                return TryReadKey(location);
            case "KEYAVAILABLE":
                EnsureArgCount(args, 0, methodName, location);
                return BooleanValue.Of(Console.KeyAvailable);
            case "ENTER":
                EnsureArgCount(args, 0, methodName, location);
                if (!_inRawMode)
                {
                    EnterRawMode();
                    _inRawMode = true;
                }
                return OslangValue.Null;
            case "EXIT":
                EnsureArgCount(args, 0, methodName, location);
                if (_inRawMode)
                {
                    ExitRawMode();
                    _inRawMode = false;
                }
                return OslangValue.Null;
            case "ALTERNATE":
                EnsureArgCount(args, 1, methodName, location);
                var enable = args[0] is BooleanValue b ? b.Value : Convert.ToBoolean(RequireNumberArg(args, 0, methodName, location));
                if (enable && !_inAlternateScreen)
                {
                    Console.Write("\x1b[?1049h");
                    _inAlternateScreen = true;
                }
                else if (!enable && _inAlternateScreen)
                {
                    Console.Write("\x1b[?1049l");
                    _inAlternateScreen = false;
                }
                return OslangValue.Null;
            case "BEGINFRAME":
                EnsureArgCount(args, 0, methodName, location);
                _frameOpen = true;
                return OslangValue.Null;
            case "ENDFRAME":
                EnsureArgCount(args, 0, methodName, location);
                _frameOpen = false;
                Console.Out.Flush();
                return OslangValue.Null;
            case "FLUSH":
                EnsureArgCount(args, 0, methodName, location);
                Console.Out.Flush();
                return OslangValue.Null;
            case "BEEP":
                EnsureArgCount(args, 0, methodName, location);
                Console.Beep();
                return OslangValue.Null;
            default:
                throw new OslangRuntimeException(location, $"Unknown OSL.CONSOLE method '{methodName}'.");
        }
    }

    public void Restore()
    {
        try
        {
            if (_inAlternateScreen)
            {
                Console.Write("\x1b[?1049l");
                _inAlternateScreen = false;
            }

            if (_inRawMode)
            {
                ExitRawMode();
                _inRawMode = false;
            }

            if (!_cursorVisible)
            {
                Console.CursorVisible = true;
                _cursorVisible = true;
            }

            Console.Write("\x1b[0m");
        }
        catch
        {
            // ignore restore errors
        }
    }

    private OslangValue ReadKeyBlocking(SourceLocation location)
    {
        var key = Console.ReadKey(true);
        return CreateKeyValue(key, location);
    }

    private OslangValue TryReadKey(SourceLocation location)
    {
        if (!Console.KeyAvailable)
        {
            return OslangValue.Null;
        }
        var key = Console.ReadKey(true);
        return CreateKeyValue(key, location);
    }

    private static OslangValue CreateKeyValue(ConsoleKeyInfo key, SourceLocation location)
    {
        string keyName;
        bool hasCtrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        bool hasAlt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        bool hasShift = (key.Modifiers & ConsoleModifiers.Shift) != 0;

        if (key.Key == ConsoleKey.Enter)
        {
            keyName = "ENTER";
        }
        else if (key.Key == ConsoleKey.Escape)
        {
            keyName = "ESC";
        }
        else if (key.Key == ConsoleKey.Tab)
        {
            keyName = "TAB";
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            keyName = "BACKSPACE";
        }
        else if (key.Key == ConsoleKey.Delete)
        {
            keyName = "DELETE";
        }
        else if (key.Key == ConsoleKey.Insert)
        {
            keyName = "INSERT";
        }
        else if (key.Key == ConsoleKey.Spacebar)
        {
            keyName = "SPACE";
        }
        else if (key.Key == ConsoleKey.UpArrow)
        {
            keyName = "UP";
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
            keyName = "DOWN";
        }
        else if (key.Key == ConsoleKey.LeftArrow)
        {
            keyName = "LEFT";
        }
        else if (key.Key == ConsoleKey.RightArrow)
        {
            keyName = "RIGHT";
        }
        else if (key.Key == ConsoleKey.Home)
        {
            keyName = "HOME";
        }
        else if (key.Key == ConsoleKey.End)
        {
            keyName = "END";
        }
        else if (key.Key == ConsoleKey.PageUp)
        {
            keyName = "PAGEUP";
        }
        else if (key.Key == ConsoleKey.PageDown)
        {
            keyName = "PAGEDOWN";
        }
        else if (key.Key >= ConsoleKey.F1 && key.Key <= ConsoleKey.F12)
        {
            keyName = $"F{(int)key.Key - (int)ConsoleKey.F1 + 1}";
        }
        else if (key.Key == ConsoleKey.Pause)
        {
            keyName = "UNKNOWN";
        }
        else
        {
            keyName = "UNKNOWN";
        }

        var charValue = key.KeyChar switch
        {
            '\0' => null,
            '\r' => null,
            '\t' => null,
            _ => key.KeyChar.ToString(),
        };

        var keyEnum = new EnumValue(new NumberValue(0), "KEYCODE", keyName);
        return new KeyValue(keyEnum, charValue, hasCtrl, hasAlt, hasShift);
    }

    private static void EnterRawMode()
    {
        try
        {
            Console.TreatControlCAsInput = true;
        }
        catch
        {
            // ignore
        }
    }

    private static void ExitRawMode()
    {
        try
        {
            Console.TreatControlCAsInput = false;
        }
        catch
        {
            // ignore
        }
    }

    private static void EnsureArgCount(IReadOnlyList<OslangValue> args, int expected, string fnName, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects {expected} argument(s), got {args.Count}.");
        }
    }

    private static string RequireStringArg(IReadOnlyList<OslangValue> args, int index, string fnName, SourceLocation location)
    {
        if (index >= args.Count)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects at least {index + 1} argument(s).");
        }
        if (args[index] is not StringValue s)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects a STRING argument at position {index + 1}.");
        }
        return s.Value;
    }

    private static double RequireNumberArg(IReadOnlyList<OslangValue> args, int index, string fnName, SourceLocation location)
    {
        if (index >= args.Count)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects at least {index + 1} argument(s).");
        }
        if (args[index] is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"{fnName}() expects a NUMBER argument at position {index + 1}.");
        }
        return n.Value;
    }
}
