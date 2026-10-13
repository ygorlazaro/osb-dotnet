// File commands on drive A: (ext4): DIR, CD, TYPE. Paths accept the DOS-ish
// spellings the ext4 resolver understands ("A:\OSB\BIN", "\OSB", "OSB", ".").

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Shell;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class FileCommands
{
    // DIR [path] [/W] - long listing by default, one name per line with /W.
    public static void Dir(char* line, int len)
    {
        bool wide = false;
        int pathS = -1, pathE = -1;
        int i = 3;
        while (i < len)
        {
            while (i < len && line[i] == ' ') i++;
            if (i >= len) break;
            int s = i;
            while (i < len && line[i] != ' ') i++;
            int e = i;
            if (e > s)
            {
                if ((line[s] == '/' || line[s] == '-') && e == s + 2 &&
                    (line[s + 1] == 'W' || line[s + 1] == 'w')) { wide = true; continue; }
                if (line[s] == '/' || line[s] == '-') continue; // ignore other switches
                if (pathS < 0) { pathS = s; pathE = e; }
            }
        }
        uint ino;
        if (pathS < 0)
        {
            // No path: use the current directory. Build a char* copy of the
            // cwd bytes (the cwd is stored as bytes; the resolver reads chars).
            char* cb = stackalloc char[300];
            byte* cwdBytes = stackalloc byte[160];
            int clen = Cwd.CopyTo(cwdBytes, 160);
            int cl = 0;
            for (int k = 0; k < clen && k < 300; k++) cb[cl++] = (char)cwdBytes[k];
            byte* outbuf = stackalloc byte[300];
            int outLen = 0;
            if (!Ext4.ResolveCanonical(cb, cl, out ino, outbuf, ref outLen))
            {
                Vga.WriteLine("Path not found");
                return;
            }
        }
        else if (!Ext4.Resolve(line + pathS, pathE - pathS, out ino))
        {
            Vga.WriteLine("Path not found");
            return;
        }
        byte* din = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, din)) { Vga.WriteLine("Error reading inode"); return; }
        if (!Ext4.IsDir(Ext4.InodeGetMode(din))) { Vga.WriteLine("Not a directory"); return; }
        PrintDirectory(din, wide);
    }

    // CD [path] - change the current directory; CD alone prints it.
    public static void Cd(char* line, int len)
    {
        int i = 2;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Cwd.Print(); Vga.PutChar('\n'); return; }
        uint ino;
        byte* canon = stackalloc byte[300];
        int canonLen = 0;
        if (!Ext4.ResolveCanonical(line + i, len - i, out ino, canon, ref canonLen))
        {
            Vga.WriteLine("Invalid directory");
            return;
        }
        byte* din = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, din)) { Vga.WriteLine("Invalid directory"); return; }
        if (!Ext4.IsDir(Ext4.InodeGetMode(din))) { Vga.WriteLine("Invalid directory"); return; }
        Cwd.Set(canon, canonLen);
        Vga.WriteLine("OK");
    }

    // TYPE <file> - dump a regular file to the console.
    public static void Type(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Vga.WriteLine("TYPE [file]"); return; }
        uint ino;
        if (!Ext4.Resolve(line + i, len - i, out ino)) { Vga.WriteLine("File not found"); return; }
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) { Vga.WriteLine("Error"); return; }
        if (!Ext4.IsReg(Ext4.InodeGetMode(cin))) { Vga.WriteLine("Not a file"); return; }
        uint sz = Ext4.InodeGetSize(cin);
        byte* blk = stackalloc byte[Ext4.BlockSize];
        ulong off = 0;
        while (off < sz)
        {
            ulong lblk = off / (ulong)Ext4.BlockSize;
            int boff = (int)(off % (ulong)Ext4.BlockSize);
            uint bnum = Ext4.InodeGetBlock(cin, lblk);
            if (bnum == 0) break;
            Ext4.ReadBlock(bnum, blk);
            int take = Ext4.BlockSize - boff;
            if ((ulong)(off + (ulong)take) > sz) take = (int)(sz - off);
            for (int k = 0; k < take; k++)
            {
                byte c = blk[boff + k];
                if (c == 0) break;
                Vga.PutChar((char)c);
            }
            off += (ulong)take;
        }
        Vga.PutChar('\n');
    }

    private static void PrintDirectory(byte* din, bool wide)
    {
        uint dsize = Ext4.InodeGetSize(din);
        ulong blocks = ((ulong)dsize + (ulong)Ext4.BlockSize - 1) / (ulong)Ext4.BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[Ext4.BlockSize];
        bool any = false;
        for (ulong b = 0; b < blocks; b++)
        {
            uint bnum = Ext4.InodeGetBlock(din, b);
            if (bnum == 0) continue;
            Ext4.ReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= Ext4.BlockSize)
            {
                byte* de = blk + off;
                uint iino = Ext4.DirEntryInode(de);
                ushort rlen = Ext4.DirEntryRecLen(de);
                if (rlen == 0) break;
                byte nlen = Ext4.DirEntryNameLen(de);
                if (iino != 0 && nlen > 0)
                {
                    any = true;
                    byte* cin = stackalloc byte[256];
                    if (Ext4.ReadInode(iino, cin))
                    {
                        ushort cmode = Ext4.InodeGetMode(cin);
                        if (wide)
                        {
                            for (int k = 0; k < nlen; k++) Vga.PutChar((char)de[Layout.DirName + k]);
                            Vga.PutChar('\t');
                            if (!Ext4.IsDir(cmode)) { Vga.PutChar(' '); }
                        }
                        else
                        {
                            PrintPerms(cmode);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetLinks(cin), 4);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetUid(cin), 5);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetGid(cin), 5);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetSize(cin), 8);
                            Vga.PutChar(' ');
                            PrintUnixDate(Ext4.InodeGetMtime(cin));
                            Vga.PutChar(' ');
                            for (int k = 0; k < nlen; k++) Vga.PutChar((char)de[Layout.DirName + k]);
                            Vga.PutChar('\n');
                        }
                    }
                    else
                    {
                        for (int k = 0; k < nlen; k++) Vga.PutChar((char)de[Layout.DirName + k]);
                        if (wide) Vga.PutChar('\t');
                        else Vga.PutChar('\n');
                    }
                }
                off += rlen;
                if (off > Ext4.BlockSize - 8) break;
            }
        }
        if (wide && any) Vga.PutChar('\n');
    }

    private static void PrintPerms(ushort mode)
    {
        Vga.PutChar(Ext4.IsDir(mode) ? 'd' : '-');
        for (int g = 2; g >= 0; g--)
        {
            int bits = (mode >> (g * 3)) & 7;
            Vga.PutChar((bits & 4) != 0 ? 'r' : '-');
            Vga.PutChar((bits & 2) != 0 ? 'w' : '-');
            Vga.PutChar((bits & 1) != 0 ? 'x' : '-');
        }
    }

    // PrintDate for ext4 mtimes (always UTC: no timezone database here).
    private static void PrintUnixDate(ulong ts)
    {
        int d, mo, y, h, mi;
        FormatUnixTime(ts, out d, out mo, out y, out h, out mi);
        Vga.WriteTwo(mo); Vga.PutChar('/'); Vga.WriteTwo(d); Vga.PutChar('/'); Vga.WriteDec((ulong)y);
        Vga.PutChar(' '); Vga.WriteTwo(h); Vga.PutChar(':'); Vga.WriteTwo(mi);
    }

    // Unix days -> civil date (Howard Hinnant's civil_from_days, inlined).
    private static void FormatUnixTime(ulong ts, out int day, out int mon, out int year, out int hh, out int mm)
    {
        ulong days = ts / 86400UL;
        ulong rem = ts % 86400UL;
        hh = (int)(rem / 3600UL);
        mm = (int)((rem % 3600UL) / 60UL);
        ulong z = days + 719468UL;
        ulong era = z / 146097UL;
        ulong doe = z - era * 146097UL;
        ulong yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        long y = (long)yoe + (long)era * 400;
        ulong doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        ulong mp = (5 * doy + 2) / 153;
        day = (int)(doy - (153 * mp + 2) / 5 + 1);
        mon = (int)(mp < 10 ? mp + 3 : mp - 9);
        year = (int)(y + (mon <= 2 ? 1 : 0));
    }
}