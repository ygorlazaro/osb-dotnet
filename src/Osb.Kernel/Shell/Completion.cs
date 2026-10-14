// TAB completion for the shell.
//
// What gets completed depends on the token being typed:
//   * first token                  -> command name (command table below)
//   * a token starting with '/'    -> a flag of that command
//   * anything else                -> a file/directory name, resolved against
//                                     the ext4 tree (drive A:), supporting
//                                     relative paths and "A:\DIR\PR..."
//
// Completion echoes itself: a single match appends only the characters the
// user has not typed (plus a '\' for a directory); several matches are listed
// and the token is extended to their longest common prefix, after which the
// prompt and the line are redrawn.
//
// The command table is filled from literals in InitCommands: managed arrays do
// not exist in the freestanding runtime, so the table is a fixed struct like
// the history ring and the environment store.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell;

internal static unsafe class Completion
{
    private const int CmdNameCap = 12;      // max chars per command name
    private const int CmdCount = 56;        // commands in the table below

    private unsafe struct CmdTable
    {
        public fixed char names[CmdCount * CmdNameCap];
        public fixed int lens[CmdCount];
        public int ready;
    }

    private static CmdTable _cmds;

    // TAB in the edit buffer: completes the token at the caret.
    public static void Complete(char* line, ref int len)
    {
        if (_cmds.ready == 0) { InitCommands(); _cmds.ready = 1; }

        // token boundaries: from just after the last space
        int ts = 0;
        int spaces = 0;
        for (int k = 0; k < len; k++)
        {
            if (line[k] == ' ') { ts = k + 1; spaces++; }
        }
        int tl = len - ts;
        char* tok = line + ts;

        if (spaces == 0) { CompleteCommand(line, ts, tok, tl, ref len); return; }
        if (tl > 0 && tok[0] == '/') { CompleteFlag(line, ts, tok, tl, ref len); return; }
        CompleteFile(line, ts, tok, tl, ref len);
    }

    // ---- command names ----

    private static void CompleteCommand(char* line, int ts, char* tok, int tl, ref int len)
    {
        if (tl == 0) return;                    // nothing typed: nothing to do
        if (tl > CmdNameCap) tl = CmdNameCap;

        char* first = stackalloc char[CmdNameCap];
        int firstLen = 0;
        int count = 0;
        for (int i = 0; i < CmdCount; i++)
        {
            if (!NameMatches(i, tok, tl)) continue;
            if (count == 0) firstLen = CopyCmdName(i, first);
            count++;
        }
        if (count == 0) return;
        if (count == 1)
        {
            ApplySuffix(line, ref len, ts, tl, first, firstLen);
            return;
        }

        // several matches: list them, extend to the longest common prefix
        char* nm = stackalloc char[CmdNameCap];
        int lcp = firstLen;
        Vga.PutChar('\n');
        for (int i = 0; i < CmdCount; i++)
        {
            if (!NameMatches(i, tok, tl)) continue;
            int nl = _cmds.lens[i];
            for (int k = 0; k < nl; k++) nm[k] = _cmds.names[i * CmdNameCap + k];
            if (count > 0) Vga.Write("  ");
            Vga.WriteStr(nm, nl);
            int c = 0;
            while (c < lcp && c < nl && ShellText.Up(nm[c]) == ShellText.Up(first[c])) c++;
            lcp = c;
        }
        Vga.PutChar('\n');

        // apply the common prefix and redraw the line under the prompt
        if (lcp > tl)
        {
            for (int k = tl; k < lcp; k++) line[ts + k] = first[k];
            len = ts + lcp;
        }
        Prompt.Write();
        Vga.WriteStr(line, len);
    }

    private static bool NameMatches(int i, char* tok, int tl)
    {
        int nl = _cmds.lens[i];
        if (nl < tl) return false;
        for (int k = 0; k < tl; k++)
            if (ShellText.Up(tok[k]) != ShellText.Up(_cmds.names[i * CmdNameCap + k])) return false;
        return true;
    }

    // ---- flags ----

    private static void CompleteFlag(char* line, int ts, char* tok, int tl, ref int len)
    {
        // Which command does this flag belong to? The first token of the line.
        int cend = 0;
        while (cend < len && line[cend] != ' ') cend++;
        if (cend == 0) return;
        char* cmd = stackalloc char[16];
        int cl = cend < 16 ? cend : 16;
        for (int k = 0; k < cl; k++) cmd[k] = ShellText.Up(line[k]);

        // The only flag in the shell today: LN /S (symbolic link).
        if (!LitEq(cmd, cl, "LN") && !LitEq(cmd, cl, "LNK")) return;

        // complete the flag body after '/' against "S"
        if (tl < 1) return;
        char* body = tok + 1;
        int bl = tl - 1;
        if (bl > 1) return;
        if (bl == 1 && ShellText.Up(body[0]) != 'S') return;

        char* full = stackalloc char[2];
        full[0] = '/'; full[1] = 'S';
        ApplySuffix(line, ref len, ts, tl, full, 2);
    }

    // ---- file and directory names ----

    private static void CompleteFile(char* line, int ts, char* tok, int tl, ref int len)
    {
        if (!Ext4.Mount()) return;

        // split the token at the last separator: directory part + name prefix
        int split = -1;
        for (int k = 0; k < tl; k++)
            if (tok[k] == '\\' || tok[k] == '/') split = k;
        char* namePrefix = tok + split + 1;
        int npLen = tl - split - 1;
        if (npLen > 31) return;

        // directory to list: the prefix, the drive root, or the current dir
        char* dirPath = stackalloc char[160];
        int dlen = 0;
        if (split < 0)
        {
            byte* cwd = stackalloc byte[160];
            int cl = Cwd.CopyTo(cwd, 160);
            for (int k = 0; k < cl; k++) dirPath[k] = (char)cwd[k];
            dlen = cl;
        }
        else if (split == 0)
        {
            dirPath[0] = '\\';
            dlen = 1;
        }
        else
        {
            for (int k = 0; k < split; k++) dirPath[k] = tok[k];
            dlen = split;
        }

        uint dirIno;
        if (!Ext4.Resolve(dirPath, dlen, out dirIno)) return;
        byte* din = stackalloc byte[256];
        if (!Ext4.ReadInode(dirIno, din)) return;
        if (!Ext4.IsDir(Ext4.InodeGetMode(din))) return;

        uint dsize = Ext4.InodeGetSize(din);
        ulong blocks = ((ulong)dsize + (ulong)Ext4.BlockSize - 1) / (ulong)Ext4.BlockSize;
        if (blocks == 0) blocks = 1;

        const int MaxMatches = 64;
        const int NameCap = 32;
        char* names = stackalloc char[MaxMatches * NameCap];
        int* nls = stackalloc int[MaxMatches];
        uint* inos = stackalloc uint[MaxMatches];
        int count = 0;
        char* first = stackalloc char[NameCap];
        int firstLen = 0;
        byte* blk = stackalloc byte[Ext4.BlockSize];

        for (ulong b = 0; b < blocks && count < MaxMatches; b++)
        {
            uint bnum = Ext4.InodeGetBlock(din, b);
            if (bnum == 0) continue;
            Ext4.ReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= Ext4.BlockSize && count < MaxMatches)
            {
                byte* de = blk + off;
                uint iino = Ext4.DirEntryInode(de);
                ushort rlen = Ext4.DirEntryRecLen(de);
                if (rlen == 0) break;
                byte nlen = Ext4.DirEntryNameLen(de);
                if (iino != 0 && nlen > 0 && nlen < NameCap)
                {
                    bool dotty = (nlen == 1 && de[Layout.DirName] == (byte)'.') ||
                                 (nlen == 2 && de[Layout.DirName] == (byte)'.' &&
                                                  de[Layout.DirName + 1] == (byte)'.');
                    bool match = !dotty && nlen >= npLen;
                    if (match)
                    {
                        for (int k = 0; k < npLen; k++)
                        {
                            char a = namePrefix[k];
                            if (a >= 'a' && a <= 'z') a -= (char)32;
                            char c2 = (char)de[Layout.DirName + k];
                            if (c2 >= 'a' && c2 <= 'z') c2 -= (char)32;
                            if (a != c2) { match = false; break; }
                        }
                    }
                    if (match)
                    {
                        for (int k = 0; k < nlen; k++) names[count * NameCap + k] = (char)de[Layout.DirName + k];
                        nls[count] = nlen;
                        inos[count] = iino;
                        if (count == 0)
                        {
                            firstLen = nlen;
                            for (int k = 0; k < nlen; k++) first[k] = (char)de[Layout.DirName + k];
                        }
                        count++;
                    }
                }
                off += rlen;
                if (off > Ext4.BlockSize - 8) break;
            }
        }

        if (count == 0) return;

        int baseStart = ts + tl;               // end of the token in the line
        if (count == 1)
        {
            AppendBaseSuffix(line, ref len, baseStart, names, npLen, nls[0], true);
            // directories get a trailing '\' so the user can keep typing
            byte* cin = stackalloc byte[256];
            if (Ext4.ReadInode(inos[0], cin) && Ext4.IsDir(Ext4.InodeGetMode(cin)))
            {
                if (len < 126) { line[len] = '\\'; Vga.PutChar('\\'); len++; }
            }
            return;
        }

        // several matches: list them, extend to the longest common prefix
        int lcp = nls[0];
        Vga.PutChar('\n');
        for (int i = 0; i < count; i++)
        {
            if (i > 0) Vga.Write("  ");
            Vga.WriteStr(names + i * NameCap, nls[i]);
            int c = 0;
            while (c < lcp && c < nls[i] &&
                   ShellText.Up(names[i * NameCap + c]) == ShellText.Up(first[c])) c++;
            lcp = c;
        }
        Vga.PutChar('\n');

        // extend the base name to the common prefix (silently), then redraw
        AppendBaseSuffix(line, ref len, baseStart, first, npLen, lcp, false);
        Prompt.Write();
        Vga.WriteStr(line, len);
    }

    // Appends name[have..nl) at `pos` in the line. When `echo` is true the
    // appended characters are printed (single match); when false the line is
    // only updated so the caller can redraw it (multi-match listing).
    private static void AppendBaseSuffix(char* line, ref int len, int pos, char* name, int have, int nl, bool echo)
    {
        for (int k = have; k < nl && pos < 126; k++)
        {
            line[pos++] = name[k];
            if (echo) Vga.PutChar(name[k]);
        }
        len = pos;
    }

    // ---- shared helpers ----

    // Extends the current token (at ts, length oldTl) with the head of `name`,
    // echoing only the characters the user has not typed yet.
    private static void ApplySuffix(char* line, ref int len, int ts, int oldTl, char* name, int nl)
    {
        if (nl < oldTl) nl = oldTl;
        if (nl > 126) nl = 126;
        if (ts + nl > 126) nl = 126 - ts;
        for (int k = oldTl; k < nl; k++)
        {
            line[ts + k] = name[k];
            Vga.PutChar(name[k]);
        }
        len = ts + nl;
    }

    private static bool LitEq(char* s, int len, string lit)
    {
        if (len != lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < len; i++)
                if (s[i] != p[i]) return false;
        }
        return true;
    }

    // ---- the command table (must mirror the dispatch in Shell.Execute) ----

    private static void Set(int i, string name)
    {
        int o = i * CmdNameCap;
        fixed (char* p = name)
        {
            for (int k = 0; k < name.Length; k++) _cmds.names[o + k] = p[k];
        }
        _cmds.lens[i] = name.Length;
    }

    private static void InitCommands()
    {
        int i = 0;
        Set(i++, "VER"); Set(i++, "ABOUT"); Set(i++, "HELP"); Set(i++, "CLS");
        Set(i++, "CLEAR"); Set(i++, "MEM"); Set(i++, "HALT"); Set(i++, "POWEROFF");
        Set(i++, "SHUTDOWN"); Set(i++, "EXIT"); Set(i++, "QUIT"); Set(i++, "REBOOT");
        Set(i++, "TIME"); Set(i++, "DATE"); Set(i++, "TICKS"); Set(i++, "SLEEP");
        Set(i++, "ECHO"); Set(i++, "SET"); Set(i++, "DIR"); Set(i++, "CD");
        Set(i++, "PWD"); Set(i++, "TYPE"); Set(i++, "DEL"); Set(i++, "ERASE");
        Set(i++, "DELETE"); Set(i++, "MD"); Set(i++, "MKDIR"); Set(i++, "RD");
        Set(i++, "RMDIR"); Set(i++, "RDDR"); Set(i++, "REN"); Set(i++, "RENAME");
        Set(i++, "MOVE"); Set(i++, "COPY"); Set(i++, "CP"); Set(i++, "SIZE");
        Set(i++, "COLOR"); Set(i++, "LN"); Set(i++, "LNK"); Set(i++, "OSLANG");
        Set(i++, "DEMO"); Set(i++, "TASK"); Set(i++, "PS"); Set(i++, "KILL");
        Set(i++, "WAIT"); Set(i++, "TAB"); Set(i++, "SPAWN"); Set(i++, "RING3");
        Set(i++, "TRASH"); Set(i++, "GREP");
        Set(i++, "HISTORY"); Set(i++, "USER"); Set(i++, "LOGIN"); Set(i++, "LOGOUT");
        Set(i++, "EXIT"); Set(i++, "WHOAMI"); Set(i++, "CHOWN"); Set(i++, "CHMOD"); Set(i++, "SUDO");
    }

    private static int CopyCmdName(int i, char* dst)
    {
        int nl = _cmds.lens[i];
        for (int k = 0; k < nl; k++) dst[k] = _cmds.names[i * CmdNameCap + k];
        return nl;
    }
}
