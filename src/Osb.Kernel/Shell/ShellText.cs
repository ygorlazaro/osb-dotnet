// Fixed-size buffers instead of managed arrays: the freestanding AOT runtime
// has no bounds-check helper, so the shell uses plain char* buffers and
// literal comparisons the way the rest of the kernel does.

namespace Osb.Kernel.Shell;

using Osb.Kernel.Hardware;

internal static unsafe class ShellText
{
    // Case-insensitive equality against a literal.
    public static bool Eq(char* line, int len, string lit)
    {
        if (len != lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < len; i++)
            {
                char a = line[i];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (a != p[i]) return false;
            }
        }
        return true;
    }

    // Case-insensitive prefix match.
    public static bool StartsWith(char* line, int len, string lit)
    {
        if (len < lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < lit.Length; i++)
            {
                char a = line[i];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (a != p[i]) return false;
            }
        }
        return true;
    }

    // Case-insensitive match of `lit` starting at line[at].
    public static bool StartsWithAt(char* line, int len, int at, string lit)
    {
        if (at < 0 || at + lit.Length > len) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < lit.Length; i++)
                if (Up(line[at + i]) != p[i]) return false;
        }
        return true;
    }

    public static char Up(char c)
    {
        return (c >= 'a' && c <= 'z') ? (char)(c - 32) : c;
    }

    // First unsigned integer at `start` (0 when there is none).
    public static int ParseIntAt(char* line, int start, int len)
    {
        int i = start;
        while (i < len && line[i] == ' ') i++;
        int v = 0;
        while (i < len && line[i] >= '0' && line[i] <= '9')
        {
            v = v * 10 + (line[i] - '0');
            i++;
        }
        return v;
    }

    // Right-aligned number in a fixed-width column.
    public static void WriteDecPad(ulong v, int width)
    {
        int digits = 1;
        ulong t = v;
        while (t >= 10) { t /= 10; digits++; }
        for (int i = digits; i < width; i++) Vga.PutChar(' ');
        Vga.WriteDec(v);
    }
}