// GREP [/I] <pattern> [file] - prints the lines that contain <pattern>.
//
// With [file]: filters that file's lines. Without it: filters the piped
// input (COMMAND | GREP <pattern>), which the shell appends after the
// first '\n' of this command's line. /I matches case-insensitively
// (the default is case-sensitive). A missing pattern prints usage.

using Osb.Kernel.Hardware;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class GrepCommand
{
    // File content scratch (static: the fs layer runs deep in the stack).
    // _file holds raw ext4 bytes; _chars is the char view for Filter.
    private unsafe struct FileBuf { public fixed byte data[4096]; }
    private static FileBuf _file;
    private unsafe struct CharBuf { public fixed char data[4096]; }
    private static CharBuf _chars;

    public static void Run(char* line, int len)
    {
        // Args end at the first '\n': everything after is piped input.
        int argEnd = len;
        for (int k = 0; k < len; k++)
            if (line[k] == '\n') { argEnd = k; break; }

        bool ignoreCase = false;
        bool havePat = false;
        char* pat = stackalloc char[128];
        int plen = 0;
        bool haveFile = false;
        char* file = stackalloc char[128];
        int flen = 0;

        int i = 4;
        while (i < argEnd)
        {
            while (i < argEnd && line[i] == ' ') i++;
            if (i >= argEnd) break;
            int s = i;
            while (i < argEnd && line[i] != ' ') i++;
            int tl = i - s;
            if (tl == 2 && (line[s] == '/' || line[s] == '-') &&
                (line[s + 1] == 'I' || line[s + 1] == 'i'))
            {
                ignoreCase = true;
            }
            else if (!havePat)
            {
                havePat = true;
                plen = tl < 127 ? tl : 127;
                for (int k = 0; k < plen; k++) pat[k] = line[s + k];
            }
            else if (!haveFile)
            {
                haveFile = true;
                flen = tl < 127 ? tl : 127;
                for (int k = 0; k < flen; k++) file[k] = line[s + k];
            }
        }

        if (!havePat) { Vga.WriteLine("Usage: GREP [/I] <pattern> [file]"); return; }

        if (haveFile)
        {
            uint ino;
            if (!Ext4.Resolve(file, flen, out ino)) { Vga.WriteLine("File not found"); return; }
            byte* din = stackalloc byte[256];
            if (!Ext4.ReadInode(ino, din) || !Ext4.IsReg(Ext4.InodeGetMode(din))) { Vga.WriteLine("Not a file"); return; }
            fixed (byte* data = _file.data)
            fixed (char* chars = _chars.data)
            {
                int got = 0;
                if (!Ext4.ReadFileAll(ino, data, 4096, out got) || got <= 0) { Vga.WriteLine("(empty)"); return; }
                for (int k = 0; k < got; k++) chars[k] = (char)data[k];
                Filter(chars, got, pat, plen, ignoreCase);
            }
            return;
        }

        // Piped input: the text after the first '\n'.
        char* input = stackalloc char[4096];
        int ilen = 0;
        for (int k = argEnd + 1; k < len && ilen < 4095; k++) input[ilen++] = line[k];
        if (ilen == 0) { Vga.WriteLine("Usage: GREP [/I] <pattern> [file]"); return; }
        Filter(input, ilen, pat, plen, ignoreCase);
    }

    // Prints each line of buf[0..len) that contains pat[0..plen).
    private static unsafe void Filter(char* buf, int len, char* pat, int plen, bool ic)
    {
        int pos = 0;
        while (pos < len)
        {
            int eol = pos;
            while (eol < len && buf[eol] != '\n') eol++;
            if (Contains(buf + pos, eol - pos, pat, plen, ic))
            {
                for (int k = pos; k < eol; k++) Vga.PutChar(buf[k]);
                Vga.PutChar('\n');
            }
            pos = eol + 1;
        }
    }

    // Substring match; ic folds ASCII case on both sides.
    private static unsafe bool Contains(char* s, int slen, char* pat, int plen, bool ic)
    {
        if (plen == 0) return true;
        if (plen > slen) return false;
        for (int i = 0; i + plen <= slen; i++)
        {
            bool ok = true;
            for (int k = 0; k < plen; k++)
            {
                char a = s[i + k];
                char b = pat[k];
                if (ic)
                {
                    if (a >= 'a' && a <= 'z') a = (char)(a - 32);
                    if (b >= 'a' && b <= 'z') b = (char)(b - 32);
                }
                if (a != b) { ok = false; break; }
            }
            if (ok) return true;
        }
        return false;
    }
}
