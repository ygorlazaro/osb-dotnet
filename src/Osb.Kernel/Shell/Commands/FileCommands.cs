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
        if (!Users.CanRead(Ext4.InodeGetUid(din), Ext4.InodeGetMode(din)))
        {
            Vga.WriteLine("Permission denied");
            return;
        }
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

    public static unsafe void Pwd(char* line, int len)
    {
        Cwd.Print();
        Vga.PutChar('\n');
    }

    // TYPE <file> - dump a regular file to the console.
    public static void Type(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Vga.WriteLine("TYPE [file]"); return; }
        uint ino;
        if (!Ext4.ResolveFollow(line + i, len - i, out ino)) { Vga.WriteLine("File not found"); return; }
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) { Vga.WriteLine("Error"); return; }
        if (!Ext4.IsReg(Ext4.InodeGetMode(cin))) { Vga.WriteLine("Not a file"); return; }
        if (!Users.CanRead(Ext4.InodeGetUid(cin), Ext4.InodeGetMode(cin)))
        {
            Vga.WriteLine("Permission denied");
            return;
        }
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
                        bool isSymlink = Ext4.IsSymlinkMode(cmode);
                        byte savedAttr = Vga.Attr;
                        if (isSymlink)
                        {
                            // symlinks in light cyan on the current background
                            Vga.SetColor(11, (byte)((savedAttr >> 4) & 0x0F));
                        }
                        if (wide)
                        {
                            for (int k = 0; k < nlen; k++) Vga.PutChar((char)de[Layout.DirName + k]);
                            if (isSymlink)
                            {
                                char* lnkTarget = stackalloc char[128];
                                int tlen = 0;
                                if (Ext4.ReadSymlink(iino, lnkTarget, 128, out tlen) && tlen > 0)
                                {
                                    Vga.Write(" -> ");
                                    for (int k = 0; k < tlen; k++) Vga.PutChar(lnkTarget[k]);
                                }
                            }
                            Vga.PutChar('\t');
                            if (!Ext4.IsDir(cmode)) { Vga.PutChar(' '); }
                        }
                        else
                        {
                            PrintPerms(cmode);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetLinks(cin), 4);
                            PrintOwner(Ext4.InodeGetUid(cin), 5);
                            PrintOwner(Ext4.InodeGetGid(cin), 5);
                            ShellText.WriteDecPad((ulong)Ext4.InodeGetSize(cin), 8);
                            Vga.PutChar(' ');
                            PrintUnixDate(Ext4.InodeGetMtime(cin));
                            Vga.PutChar(' ');
                            for (int k = 0; k < nlen; k++) Vga.PutChar((char)de[Layout.DirName + k]);
                            if (isSymlink)
                            {
                                char* lnkTarget = stackalloc char[128];
                                int tlen = 0;
                                if (Ext4.ReadSymlink(iino, lnkTarget, 128, out tlen) && tlen > 0)
                                {
                                    Vga.Write(" -> ");
                                    for (int k = 0; k < tlen; k++) Vga.PutChar(lnkTarget[k]);
                                }
                            }
                            Vga.PutChar('\n');
                        }
                        if (isSymlink) Vga.SetAttr(savedAttr);
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

    // ls-style owner column: the user name when the uid is known, else the
    // bare number (the gid column shows the same name: single-user files).
    private static void PrintOwner(uint uid, int width)
    {
        char* nm = stackalloc char[Osb.Kernel.Shell.Users.NameCap];
        int len;
        if (Osb.Kernel.Shell.Users.NameOf(uid, nm, out len))
        {
            Vga.WriteStr(nm, len);
            for (int k = len; k < width; k++) Vga.PutChar(' ');
            return;
        }
        ShellText.WriteDecPad(uid, width);
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


    // ---- Token helpers (whitespace-delimited, simple quoted strings) ----
    private static unsafe int Tokenize(char* line, int len, int maxTokens, char** toks, int* tokLens)
    {
        unchecked
        {
        int n = 0;
        int i = 0;
        while (i < len && n < maxTokens)
        {
            while (i < len && line[i] == ' ') i++;
            if (i >= len) break;
            int s;
            int e;
            if (line[i] == '"')
            {
                i++;
                s = i;
                while (i < len && line[i] != '"') i++;
                e = i;
                if (i < len) i++; // skip closing quote
            }
            else
            {
                s = i;
                while (i < len && line[i] != ' ') i++;
                e = i;
            }
            if (e > s)
            {
                toks[n] = line + s;
                tokLens[n] = e - s;
                n++;
            }
        }
        return n;
    }

        }

    // Write permission on the directory that receives (or loses) an entry:
    // the unix rule — you may create/remove inside a directory you can write.
    private static unsafe bool ParentWritable(uint parentIno)
    {
        byte* pin = stackalloc byte[256];
        if (!Ext4.ReadInode(parentIno, pin)) return false;
        return Users.CanWrite(Ext4.InodeGetUid(pin), Ext4.InodeGetMode(pin));
    }

    public static unsafe bool ResolveParent(char* path, int len, out uint parentIno, char* baseName, ref int baseLen)
    {
        baseLen = 0;
        parentIno = 0;
        // trim trailing separators
        int e = len;
        while (e > 0 && (path[e - 1] == '\\' || path[e - 1] == '/')) e--;
        // find last separator
        int last = -1;
        for (int k = e - 1; k >= 0; k--)
            if (path[k] == '\\' || path[k] == '/') { last = k; break; }
        if (last < 0)
        {
            // no separator: parent is current dir
            if (!Ext4.GetCurrentDirIno(out parentIno)) return false;
            baseLen = e;
            for (int k = 0; k < baseLen; k++) baseName[k] = path[k];
            return true;
        }
        // parent path = [0,last) (includes drive if present)
        int plen = last;
        // if parent is just root ("A:" or "A:\\"), handle
        if (plen == 0)
        {
            parentIno = Ext4.RootInode;
        }
        else
        {
            char* pbuf = stackalloc char[300];
            // trim trailing separators from parent
            int pplen = plen;
            while (pplen > 0 && (path[pplen - 1] == '\\' || path[pplen - 1] == '/')) pplen--;
            if (pplen <= 3 && path[1] == ':')
            {
                // root
                parentIno = Ext4.RootInode;
            }
            else
            {
                for (int k = 0; k < pplen; k++) pbuf[k] = path[k];
                if (!Ext4.Resolve(pbuf, pplen, out parentIno)) return false;
            }
        }
        baseLen = e - (last + 1);
        for (int k = 0; k < baseLen; k++) baseName[k] = path[last + 1 + k];
        return true;
    }


    public static unsafe void Size(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Vga.WriteLine("SIZE [file]"); return; }
        int s = i;
        while (i < len && line[i] != ' ') i++;
        int el = i - s;
        char* path = line + s;
        uint ino;
        if (!Ext4.Resolve(path, el, out ino)) { Vga.WriteLine("File not found"); return; }
        byte* inBuf = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, inBuf)) { Vga.WriteLine("Error"); return; }
        if (Ext4.IsDir(Ext4.InodeGetMode(inBuf))) { Vga.WriteLine("Directory."); return; }
        Vga.Write("Size: ");
        Vga.WriteDec(Ext4.InodeGetSize(inBuf));
        Vga.WriteLine(" bytes");
    }


    public static unsafe void Del(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;
        while (i < len && line[i] == ' ') i++;
        int s = i;
        while (i < len && line[i] != ' ') i++;
        int plen = i - s;
        if (plen <= 0) { Vga.WriteLine("DEL [file]"); return; }
        char* path = line + s;

        char* baseName = stackalloc char[256];
        int baseLen = 0;
        uint parentIno;
        if (!ResolveParent(path, plen, out parentIno, baseName, ref baseLen)) { Vga.WriteLine("Path not found"); return; }
        if (baseLen <= 0) { Vga.WriteLine("Usage: DEL [file]"); return; }
        if (!ParentWritable(parentIno)) { Vga.WriteLine("Permission denied"); return; }

        uint childIno; byte childType;
        if (!Ext4.DirEntryExists(parentIno, baseName, baseLen, out childIno, out childType)) { Vga.WriteLine("File not found"); return; }
        byte* inBuf = stackalloc byte[256];
        if (!Ext4.ReadInode(childIno, inBuf)) { Vga.WriteLine("Error"); return; }
        if (Ext4.IsDir(Ext4.InodeGetMode(inBuf))) { Vga.WriteLine("Is a directory; use RD."); return; }

        // canonical path, remembered in the trash index for TRASH /U
        byte* canon = stackalloc byte[300];
        int canonLen = 0;
        uint canonIno;
        Ext4.ResolveCanonical(path, plen, out canonIno, canon, ref canonLen);

        // the file goes to the per-user trash ($HOME\TRASH); files
        // already in the trash, or deletes when the trash cannot be
        // used, free the file for real
        if (TrashCommands.MoveToTrash(parentIno, baseName, baseLen, childIno, childType, canon, canonLen))
        {
            Vga.WriteLine("OK");
            return;
        }
        HardDelete(parentIno, baseName, baseLen, childIno, inBuf);
        Vga.WriteLine("OK");
    }

    // Frees a file's blocks and inode and removes its directory entry.
    private static unsafe void HardDelete(uint parentIno, char* baseName, int baseLen, uint childIno, byte* inBuf)
    {
        uint sz = Ext4.InodeGetSize(inBuf);
        uint nb = (sz + (uint)Ext4.BlockSize - 1) / (uint)Ext4.BlockSize;
        if (nb == 0 && sz > 0) nb = 1;
        for (uint lb = 0; lb < nb; lb++)
        {
            uint pb = Ext4.InodeGetBlock(inBuf, lb);
            if (pb != 0) Ext4.FreeBlock(pb);
        }
        Ext4.FreeInode(childIno);
        Ext4.DirRemoveEntry(parentIno, baseName, baseLen);
    }


    // Creates a directory at `path` with the given mode (type bits included),
    // owned by `uid`. Prints the reason and returns false on failure.
    public static unsafe bool MakeDir(char* path, int plen, ushort mode, uint uid, out uint newIno)
    {
        newIno = 0;
        char* baseName = stackalloc char[256];
        int baseLen = 0;
        uint parentIno;
        if (!ResolveParent(path, plen, out parentIno, baseName, ref baseLen)) { Vga.WriteLine("Path not found"); return false; }
        if (baseLen <= 0) { Vga.WriteLine("Usage: MD [directory]"); return false; }
        if (!ParentWritable(parentIno)) { Vga.WriteLine("Permission denied"); return false; }

        uint cino; byte ctype;
        if (Ext4.DirEntryExists(parentIno, baseName, baseLen, out cino, out ctype)) { Vga.WriteLine("Already exists"); return false; }

        if (!Ext4.AllocInode(out newIno, mode)) { Vga.WriteLine("No free inode"); return false; }

        uint dirBlock;
        if (!Ext4.AllocBlock(out dirBlock)) { Ext4.FreeInode(newIno); Vga.WriteLine("No free block"); return false; }
        byte* db = stackalloc byte[Ext4.BlockSize];
        for (int k = 0; k < Ext4.BlockSize; k++) db[k] = 0;
        Ext4.Wr32(db, Layout.DirIno, newIno);
        Ext4.Wr16(db, Layout.DirRecLen, 12);
        db[Layout.DirNameLen] = 1;
        db[Layout.DirType] = 2;
        db[Layout.DirName] = (byte)'.';
        int o = 12;
        Ext4.Wr32(db, o + Layout.DirIno, parentIno);
        Ext4.Wr16(db, o + Layout.DirRecLen, (ushort)(Ext4.BlockSize - 12));
        db[o + Layout.DirNameLen] = 2;
        db[o + Layout.DirType] = 2;
        db[o + Layout.DirName] = (byte)'.';
        db[o + Layout.DirName + 1] = (byte)'.';
        Ext4.WriteBlock(dirBlock, db);

        byte* inBuf = stackalloc byte[256];
        for (int k = 0; k < 256; k++) inBuf[k] = 0;
        Ext4.Wr16(inBuf, Layout.InoMode, mode);
        Ext4.Wr16(inBuf, Layout.InoUid, (ushort)uid);
        Ext4.Wr16(inBuf, Layout.InoLinks, 2);
        Ext4.Wr32(inBuf, Layout.InoSizeLo, Ext4.BlockSize);
        Ext4.Wr32(inBuf, Layout.InoSizeHi, 0);
        Ext4.Wr32(inBuf, Layout.InoBlocksLo, 8);
        Ext4.Wr16(inBuf, Layout.InoBlocksHi, 0);
        Ext4.Wr32(inBuf, Layout.InoBlock + 0 * 4, dirBlock);
        Ext4.WriteInode(newIno, inBuf);

        byte* pin = stackalloc byte[256];
        Ext4.ReadInode(parentIno, pin);
        uint links = Ext4.InodeGetLinks(pin);
        Ext4.Wr16(pin, Layout.InoLinks, (ushort)(links + 1));
        Ext4.WriteInode(parentIno, pin);

        if (Ext4.DirAddEntry(parentIno, baseName, baseLen, newIno, 2)) return true;
        Ext4.FreeInode(newIno); Ext4.FreeBlock(dirBlock);
        Vga.WriteLine("Error adding entry");
        return false;
    }

    public static unsafe void MkDir(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;
        while (i < len && line[i] == ' ') i++;
        int s = i;
        while (i < len && line[i] != ' ') i++;
        int plen = i - s;
        if (plen <= 0) { Vga.WriteLine("MD [directory]"); return; }
        char* path = line + s;

        // the new folder is owned by the current user, mode 0755 (like unix)
        if (MakeDir(path, plen, (ushort)(0x4000 | 0x1ED), (uint)Users.Uid, out _))
            Vga.WriteLine("OK");
    }

    // ---- RD / RMDIR ----
    // Removes an empty directory at `path`. Prints the reason and returns false
    // on failure (not a directory, not empty, missing).
    public static unsafe bool RemoveDir(char* path, int len)
    {
        char* baseName = stackalloc char[256];
        int baseLen = 0;
        uint parentIno;
        if (!ResolveParent(path, len, out parentIno, baseName, ref baseLen)) { Vga.WriteLine("Path not found"); return false; }
        if (baseLen <= 0) { Vga.WriteLine("Usage: RD [directory]"); return false; }
        if (!ParentWritable(parentIno)) { Vga.WriteLine("Permission denied"); return false; }

        uint cino; byte ctype;
        if (!Ext4.DirEntryExists(parentIno, baseName, baseLen, out cino, out ctype)) { Vga.WriteLine("Not found"); return false; }
        byte* inBuf = stackalloc byte[256];
        Ext4.ReadInode(cino, inBuf);
        if (!Ext4.IsDir(Ext4.InodeGetMode(inBuf))) { Vga.WriteLine("Not a directory"); return false; }

        bool empty;
        if (!Ext4.DirIsEmpty(cino, out empty) || !empty) { Vga.WriteLine("Directory not empty"); return false; }

        uint sz = Ext4.InodeGetSize(inBuf);
        uint nb = (sz + (uint)Ext4.BlockSize - 1) / (uint)Ext4.BlockSize;
        if (nb == 0 && sz > 0) nb = 1;
        for (uint lb = 0; lb < nb; lb++)
        {
            uint pb = Ext4.InodeGetBlock(inBuf, lb);
            if (pb != 0) Ext4.FreeBlock(pb);
        }
        byte* pin = stackalloc byte[256];
        Ext4.ReadInode(parentIno, pin);
        uint links = Ext4.InodeGetLinks(pin);
        if (links > 1) Ext4.Wr16(pin, Layout.InoLinks, (ushort)(links - 1));
        Ext4.WriteInode(parentIno, pin);

        Ext4.FreeInode(cino);
        if (Ext4.DirRemoveEntry(parentIno, baseName, baseLen)) return true;
        Vga.WriteLine("Error removing entry");
        return false;
    }

    public static unsafe void RdDir(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;
        while (i < len && line[i] == ' ') i++;
        int s = i;
        while (i < len && line[i] != ' ') i++;
        int plen = i - s;
        if (plen <= 0) { Vga.WriteLine("RD [directory]"); return; }
        char* path = line + s;

        if (RemoveDir(path, plen)) Vga.WriteLine("OK");
    }

    public static unsafe void Ren(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++; // skip command
        while (i < len && line[i] == ' ') i++;
        int s1 = i;
        while (i < len && line[i] != ' ') i++;
        int l1 = i - s1;
        if (l1 <= 0) { Vga.WriteLine("REN old new"); return; }
        while (i < len && line[i] == ' ') i++;
        int s2 = i;
        while (i < len && line[i] != ' ') i++;
        int l2 = i - s2;
        if (l2 <= 0) { Vga.WriteLine("REN old new"); return; }

        char* oldPath = line + s1;
        int oldLen = l1;
        char* newPath = line + s2;
        int newLen = l2;

        char* oldBase = stackalloc char[256];
        int oldBaseLen = 0;
        uint oldParent;
        if (!ResolveParent(oldPath, oldLen, out oldParent, oldBase, ref oldBaseLen)) { Vga.WriteLine("Path not found"); return; }
        if (!ParentWritable(oldParent)) { Vga.WriteLine("Permission denied"); return; }

        uint childIno; byte childType;
        if (!Ext4.DirEntryExists(oldParent, oldBase, oldBaseLen, out childIno, out childType)) { Vga.WriteLine("Not found"); return; }

        char* newBase = stackalloc char[256];
        int newBaseLen = 0;
        uint newParent;
        if (!ResolveParent(newPath, newLen, out newParent, newBase, ref newBaseLen)) { Vga.WriteLine("Path not found"); return; }
        if (newBaseLen <= 0) { Vga.WriteLine("Usage: REN old new"); return; }
        if (newParent != oldParent && !ParentWritable(newParent)) { Vga.WriteLine("Permission denied"); return; }

        uint existingIno; byte existingType;
        if (Ext4.DirEntryExists(newParent, newBase, newBaseLen, out existingIno, out existingType)) { Vga.WriteLine("Target already exists"); return; }

        // move across directories
        if (newParent != oldParent)
        {
            byte* inBuf = stackalloc byte[256];
            Ext4.ReadInode(childIno, inBuf);
            if (Ext4.IsDir(Ext4.InodeGetMode(inBuf))) { Vga.WriteLine("Cannot move directory across dirs"); return; }

            uint newIno;
            if (!Ext4.AllocInode(out newIno, 0x81A4)) { Vga.WriteLine("No free inode"); return; }
            Ext4.CopyFileData(childIno, newIno);
            if (!Ext4.DirAddEntry(newParent, newBase, newBaseLen, newIno, 1)) { Ext4.FreeInode(newIno); Vga.WriteLine("Error"); return; }

            byte* oldBuf = stackalloc byte[256];
            Ext4.ReadInode(childIno, oldBuf);
            uint oldSz = Ext4.InodeGetSize(oldBuf);
            uint onb = (oldSz + (uint)Ext4.BlockSize - 1) / (uint)Ext4.BlockSize;
            if (onb == 0 && oldSz > 0) onb = 1;
            for (uint lb = 0; lb < onb; lb++)
            {
                uint pb = Ext4.InodeGetBlock(oldBuf, lb);
                if (pb != 0) Ext4.FreeBlock(pb);
            }
            Ext4.FreeInode(childIno);
            Ext4.DirRemoveEntry(oldParent, oldBase, oldBaseLen);
            Vga.WriteLine("OK");
            return;
        }

        // same directory rename
        if (!Ext4.DirRemoveEntry(oldParent, oldBase, oldBaseLen)) { Vga.WriteLine("Error"); return; }
        if (!Ext4.DirAddEntry(oldParent, newBase, newBaseLen, childIno, childType)) { Ext4.DirAddEntry(oldParent, oldBase, oldBaseLen, childIno, childType); Vga.WriteLine("Error"); return; }
        Vga.WriteLine("OK");
    }

    // ---- COPY src dst ----
    public static unsafe void Copy(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++; // skip command
        while (i < len && line[i] == ' ') i++;
        int s1 = i;
        while (i < len && line[i] != ' ') i++;
        int l1 = i - s1;
        if (l1 <= 0) { Vga.WriteLine("COPY src dst"); return; }
        while (i < len && line[i] == ' ') i++;
        int s2 = i;
        while (i < len && line[i] != ' ') i++;
        int l2 = i - s2;
        if (l2 <= 0) { Vga.WriteLine("COPY src dst"); return; }

        char* srcPath = line + s1;
        int srcLen = l1;
        uint srcIno;
        if (!Ext4.Resolve(srcPath, srcLen, out srcIno)) { Vga.WriteLine("Source not found"); return; }
        byte* sin = stackalloc byte[256];
        Ext4.ReadInode(srcIno, sin);
        if (Ext4.IsDir(Ext4.InodeGetMode(sin))) { Vga.WriteLine("Cannot copy directory"); return; }
        if (!Users.CanRead(Ext4.InodeGetUid(sin), Ext4.InodeGetMode(sin)))
        {
            Vga.WriteLine("Permission denied");
            return;
        }

        char* dstPath = line + s2;
        int dstLen = l2;

        // src basename
        char* srcBase = stackalloc char[256];
        int srcBaseLen = 0;
        {
            int e = srcLen;
            while (e > 0 && (srcPath[e - 1] == '\\' || srcPath[e - 1] == '/')) e--;
            int last = -1;
            for (int k = e - 1; k >= 0; k--) if (srcPath[k] == '\\' || srcPath[k] == '/') { last = k; break; }
            for (int k = last + 1; k < e; k++) srcBase[srcBaseLen++] = srcPath[k];
            if (srcBaseLen == 0) { for (int k = 0; k < srcLen; k++) srcBase[srcBaseLen++] = srcPath[k]; }
        }

        uint dstIno;
        if (Ext4.Resolve(dstPath, dstLen, out dstIno))
        {
            byte* din = stackalloc byte[256];
            Ext4.ReadInode(dstIno, din);
            if (Ext4.IsDir(Ext4.InodeGetMode(din)))
            {
                if (!Users.CanWrite(Ext4.InodeGetUid(din), Ext4.InodeGetMode(din)))
                {
                    Vga.WriteLine("Permission denied");
                    return;
                }
                char* bname = srcBase;
                int bnameLen = srcBaseLen;
                uint ex; byte ext;
                if (Ext4.DirEntryExists(dstIno, bname, bnameLen, out ex, out ext)) { Vga.WriteLine("Target already exists"); return; }
                uint newIno;
                if (!Ext4.AllocInode(out newIno, 0x81A4)) { Vga.WriteLine("No free inode"); return; }
                Ext4.CopyFileData(srcIno, newIno);
                if (!Ext4.DirAddEntry(dstIno, bname, bnameLen, newIno, 1)) { Ext4.FreeInode(newIno); Vga.WriteLine("Error"); return; }
                Vga.WriteLine("OK");
                return;
            }
            else
            {
                Vga.WriteLine("Target already exists");
                return;
            }
        }

        char* baseName = stackalloc char[256];
        int baseLen = 0;
        uint parentIno;
        if (!ResolveParent(dstPath, dstLen, out parentIno, baseName, ref baseLen)) { Vga.WriteLine("Path not found"); return; }
        if (baseLen <= 0) { Vga.WriteLine("Usage: COPY src dst"); return; }
        if (!ParentWritable(parentIno)) { Vga.WriteLine("Permission denied"); return; }
        uint ex2; byte exType;
        if (Ext4.DirEntryExists(parentIno, baseName, baseLen, out ex2, out exType)) { Vga.WriteLine("Target already exists"); return; }

        uint newIno2;
        if (!Ext4.AllocInode(out newIno2, 0x81A4)) { Vga.WriteLine("No free inode"); return; }
        Ext4.CopyFileData(srcIno, newIno2);
        if (Ext4.DirAddEntry(parentIno, baseName, baseLen, newIno2, 1)) Vga.WriteLine("OK");
        else { Ext4.FreeInode(newIno2); Vga.WriteLine("Error"); }
    }


    public static unsafe void Ln(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++; // skip LN

        // parse first token
        while (i < len && line[i] == ' ') i++;
        int s0 = i;
        while (i < len && line[i] != ' ') i++;
        int l0 = i - s0;
        if (l0 <= 0) { Vga.WriteLine("LN [/S] source destination"); return; }

        bool symbolic = false;
        char* src = null; int srcLen = 0;
        char* second = null; int secondLen = 0;
        // if first is flag
        if (l0 == 2 && line[s0] == '/' && (line[s0 + 1] == 'S' || line[s0 + 1] == 's'))
        {
            symbolic = true;
            while (i < len && line[i] == ' ') i++;
            int s1 = i;
            while (i < len && line[i] != ' ') i++;
            int l1 = i - s1;
            if (l1 <= 0) { Vga.WriteLine("LN /S target destination"); return; }
            char* buf1 = stackalloc char[256];
            for (int k = 0; k < l1; k++) buf1[k] = line[s1 + k];
            src = buf1; srcLen = l1;
            while (i < len && line[i] == ' ') i++;
            int s2 = i;
            while (i < len && line[i] != ' ') i++;
            int l2 = i - s2;
            if (l2 <= 0) { Vga.WriteLine("LN /S target destination"); return; }
            char* buf2 = stackalloc char[256];
            for (int k = 0; k < l2; k++) buf2[k] = line[s2 + k];
            second = buf2; secondLen = l2;
        }
        else
        {
            char* buf0 = stackalloc char[256];
            for (int k = 0; k < l0; k++) buf0[k] = line[s0 + k];
            src = buf0; srcLen = l0;
            while (i < len && line[i] == ' ') i++;
            int s2 = i;
            while (i < len && line[i] != ' ') i++;
            int l2 = i - s2;
            if (l2 <= 0) { Vga.WriteLine("LN source destination"); return; }
            char* buf2 = stackalloc char[256];
            for (int k = 0; k < l2; k++) buf2[k] = line[s2 + k];
            second = buf2; secondLen = l2;
        }
        char* dst = second;
        int dstLen = secondLen;

        if (symbolic)
        {
            char* baseName = stackalloc char[256];
            int baseLen = 0;
            uint parentIno;
            if (!ResolveParent(dst, dstLen, out parentIno, baseName, ref baseLen)) { Vga.WriteLine("Path not found"); return; }
            if (baseLen <= 0) { Vga.WriteLine("Usage: LN /S target destination"); return; }
            if (!ParentWritable(parentIno)) { Vga.WriteLine("Permission denied"); return; }
            uint ex; byte exType;
            if (Ext4.DirEntryExists(parentIno, baseName, baseLen, out ex, out exType)) { Vga.WriteLine("Target already exists"); return; }
            uint linkIno;
            if (!Ext4.AllocSymlink(out linkIno, src, srcLen)) { Vga.WriteLine("Cannot create symlink"); return; }
            if (Ext4.DirAddEntry(parentIno, baseName, baseLen, linkIno, 7)) Vga.WriteLine("OK");
            else { Ext4.FreeInode(linkIno); Vga.WriteLine("Error"); }
            return;
        }

        uint srcIno;
        if (!Ext4.Resolve(src, srcLen, out srcIno)) { Vga.WriteLine("Source not found"); return; }
        byte* sin = stackalloc byte[256];
        Ext4.ReadInode(srcIno, sin);
        if (Ext4.IsDir(Ext4.InodeGetMode(sin))) { Vga.WriteLine("Cannot hardlink directory"); return; }

        char* baseName2 = stackalloc char[256];
        int baseLen2 = 0;
        uint parentIno2;
        if (!ResolveParent(dst, dstLen, out parentIno2, baseName2, ref baseLen2)) { Vga.WriteLine("Path not found"); return; }
        if (baseLen2 <= 0) { Vga.WriteLine("Usage: LN source destination"); return; }
        if (!ParentWritable(parentIno2)) { Vga.WriteLine("Permission denied"); return; }
        uint ex2; byte exType2;
        if (Ext4.DirEntryExists(parentIno2, baseName2, baseLen2, out ex2, out exType2)) { Vga.WriteLine("Target already exists"); return; }

        byte dtype = 1;
        if (Ext4.IsSymlink(sin)) dtype = 7;
        uint links = Ext4.InodeGetLinks(sin);
        Ext4.Wr16(sin, Layout.InoLinks, (ushort)(links + 1));
        Ext4.WriteInode(srcIno, sin);

        if (Ext4.DirAddEntry(parentIno2, baseName2, baseLen2, srcIno, dtype)) Vga.WriteLine("OK");
        else Vga.WriteLine("Error");
    }


    // ---- COLOR ----
    public static unsafe void Color(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++; // skip COLOR
        byte fg = 255, bg = 255;
        int count = 0;
        while (i < len && count < 2)
        {
            while (i < len && line[i] == ' ') i++;
            if (i >= len) break;
            int s = i;
            while (i < len && line[i] != ' ') i++;
            int v = 0;
            bool ok = (i - s) > 0;
            for (int k = s; k < i; k++)
            {
                if (line[k] < '0' || line[k] > '9') { ok = false; break; }
                v = v * 10 + (line[k] - '0');
            }
            if (!ok || v < 0 || v > 15) { Vga.WriteLine("Invalid color (0-15)."); return; }
            if (count == 0) fg = (byte)v; else bg = (byte)v;
            count++;
        }

        if (count == 0)
        {
            Vga.WriteLine("Color table: 0=black 1=blue 2=green 3=cyan 4=red 5=magenta 6=brown");
            Vga.WriteLine("            7=light grey 8=dark grey 9=light blue 10=light green");
            Vga.WriteLine("            11=light cyan 12=light red 13=light magenta");
            Vga.WriteLine("            14=yellow 15=white");
            Vga.WriteLine("Usage: COLOR fg bg");
            return;
        }
        if (count == 1) { Vga.WriteLine("Usage: COLOR fg bg"); return; }
        if (fg == bg) { Vga.WriteLine("Foreground and background cannot be the same."); return; }

        Vga.SetColor(fg, bg);
        Vga.Clear();

        // Saving needs a writable CONF: a non-root user still gets the color
        // for this session, it just does not persist (unix-style).
        char* confParent = stackalloc char[12];
        fixed (char* _cp = "A:\\OSB\\CONF") { for (int k = 0; k < 11; k++) confParent[k] = _cp[k]; }
        confParent[11] = '\0';
        uint parentIno;
        if (!Ext4.Resolve(confParent, 11, out parentIno)) { Vga.WriteLine("Color set."); return; }
        if (!ParentWritable(parentIno))
        {
            Vga.WriteLine("Color set (not saved: permission denied).");
            return;
        }
        Vga.WriteLine("Color set. Saved to A:\\OSB\\CONF\\COLOR.CFG");

        char* fn = stackalloc char[10];
        fixed (char* _cf = "COLOR.CFG") { for (int k = 0; k < 9; k++) fn[k] = _cf[k]; }
        fn[9] = '\0';
        int fnLen = 9;

        uint ino;
        byte dummyType;
        if (!Ext4.DirEntryExists(parentIno, fn, fnLen, out ino, out dummyType))
        {
            if (!Ext4.AllocInode(out ino, 0x81A4)) return;
            if (!Ext4.DirAddEntry(parentIno, fn, fnLen, ino, 1)) { Ext4.FreeInode(ino); return; }
        }
        char* val = stackalloc char[8];
        int vl = 0;
        if (fg >= 10) val[vl++] = (char)('0' + fg / 10);
        val[vl++] = (char)('0' + fg % 10);
        val[vl++] = ' ';
        if (bg >= 10) val[vl++] = (char)('0' + bg / 10);
        val[vl++] = (char)('0' + bg % 10);
        val[vl++] = '\n';
        byte* data = stackalloc byte[8];
        for (int k = 0; k < vl; k++) data[k] = (byte)val[k];
        Ext4.WriteFile(ino, data, vl);
    }

    public static unsafe void LoadColor()
    {
        if (!Ext4.Mount()) return;
        char* path = stackalloc char[22];
        fixed (char* _pp = "A:\\OSB\\CONF\\COLOR.CFG") { for (int k = 0; k < 21; k++) path[k] = _pp[k]; }
        path[21] = '\0';
        uint ino;
        if (!Ext4.Resolve(path, 21, out ino)) return;
        byte* buf = stackalloc byte[64];
        int got = 0;
        Ext4.ReadFileAll(ino, buf, 64, out got);
        if (got <= 0) return;
        int fg = -1, bg = -1;
        int pos = 0;
        while (pos < got)
        {
            if (buf[pos] == ' ' || buf[pos] == '\n' || buf[pos] == '\r') { pos++; continue; }
            if (buf[pos] < '0' || buf[pos] > '9') { pos++; continue; }
            int v = 0;
            while (pos < got && buf[pos] >= '0' && buf[pos] <= '9') { v = v * 10 + (buf[pos] - '0'); pos++; }
            if (fg < 0) fg = v; else if (bg < 0) bg = v;
        }
        if (fg >= 0 && fg <= 15 && bg >= 0 && bg <= 15 && fg != bg)
        {
            Vga.SetColor((byte)fg, (byte)bg);
        }
    }

}
