// Per-user trash on the ext4 volume: $HOME\TRASH.
//
// DEL moves a file into the user's trash instead of freeing
// it, so a delete can be undone. TRASH lists the trash and
// searches it (/F), and TRASH /U <name> undeletes a file
// back to where it came from. The original location is
// remembered in a small index file ($HOME\TRASH\.ORIG, one
// "name<TAB>canonical-path" line per trashed file); the
// storage side lives in TrashStore.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class TrashCommands
{
    private const int PathCap = 300;

    // ---- boot: make sure every user has $HOME and $HOME\TRASH ----

    // Creates the home folder and the trash folder of each
    // account when missing (runs as root, so it works on
    // first boot and on images from before the trash).
    public static unsafe void EnsureAll()
    {
        if (!Ext4.Mount()) return;
        int saved = Osb.Kernel.Shell.Users.CurrentIndex;
        Osb.Kernel.Shell.Users.SetSession(0);   // provisioning runs as root
        for (int i = 0; i < Osb.Kernel.Shell.Users.Count; i++)
        {
            char* home = stackalloc char[64];
            int hl = Osb.Kernel.Shell.Users.HomeAt(i, home);
            if (hl <= 0) continue;
            uint hino;
            if (!Ext4.Resolve(home, hl, out hino))
                FileCommands.MakeDir(home, hl, (ushort)(0x4000 | 0x1ED),
                    (uint)Osb.Kernel.Shell.Users.UidAt(i), out _);

            char* trash = stackalloc char[PathCap];
            int tl = 0;
            TrashStore.AppendPath(trash, ref tl, home, hl);
            TrashStore.AppendLit(trash, ref tl, "\\TRASH");
            uint tino;
            if (!Ext4.Resolve(trash, tl, out tino))
                FileCommands.MakeDir(trash, tl, (ushort)(0x4000 | 0x1ED),
                    (uint)Osb.Kernel.Shell.Users.UidAt(i), out _);
        }
        Osb.Kernel.Shell.Users.SetSession(saved);
    }

    // ---- TRASH command ----

    // TRASH [/F <pattern>]   list / search the trash
    // TRASH /U <name>        undelete back to the original location
    public static unsafe void Run(char* line, int len)
    {
        int i = 5;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { List(null, 0); return; }

        if ((line[i] == '/' || line[i] == '-') && i + 1 < len)
        {
            char flag = line[i + 1];
            if (flag >= 'a' && flag <= 'z') flag = (char)(flag - 32);
            int rest = i + 2;
            while (rest < len && line[rest] == ' ') rest++;
            if (flag == 'F')
            {
                int s = rest;
                while (rest < len && line[rest] != ' ') rest++;
                List(line + s, rest - s);
                return;
            }
            if (flag == 'U')
            {
                int s = rest;
                while (rest < len && line[rest] != ' ') rest++;
                if (rest <= s) { Vga.WriteLine("Usage: TRASH /U <name>"); return; }
                Undelete(line + s, rest - s);
                return;
            }
        }
        // bare text is a search pattern
        List(line + i, len - i);
    }

    // ---- DEL support ----

    // Moves the entry (parentIno/baseName) into the current
    // user's trash. `canon`/`canonLen` is the canonical path
    // of the file before the move, stored for /U. Returns
    // false when the trash cannot be used (the caller then
    // deletes the file for real).
    public static unsafe bool MoveToTrash(uint parentIno, char* baseName, int baseLen,
        uint childIno, byte childType, byte* canon, int canonLen)
    {
        if (!Ext4.Mount()) return false;
        char* trash = stackalloc char[PathCap];
        int tl = 0;
        if (!TrashStore.BuildTrashPath(trash, ref tl)) return false;
        uint trashIno;
        if (!Ext4.Resolve(trash, tl, out trashIno))
        {
            if (!TrashStore.EnsureTrash()) return false;
            if (!Ext4.Resolve(trash, tl, out trashIno)) return false;
        }
        if (parentIno == trashIno) return false;   // already trashed

        // pick a free name inside the trash (name, name~1, name~2, ...)
        char* name = stackalloc char[256];
        int nlen = 0;
        if (!TrashStore.PickFreeName(trashIno, baseName, baseLen, name, ref nlen)) return false;

        if (!Ext4.DirAddEntry(trashIno, name, nlen, childIno, childType)) return false;
        if (!Ext4.DirRemoveEntry(parentIno, baseName, baseLen))
        {
            Ext4.DirRemoveEntry(trashIno, name, nlen);   // rollback
            return false;
        }
        TrashStore.RecordOrigin(trashIno, name, nlen, canon, canonLen);
        return true;
    }

    // ---- internals ----

    // Lists (pattern == null/empty: everything) or searches
    // (case-insensitive substring match) the trash, with the
    // original path of each file.
    private static unsafe void List(char* pattern, int patLen)
    {
        if (!Ext4.Mount()) { Vga.WriteLine("No filesystem"); return; }
        char* trash = stackalloc char[PathCap];
        int tl = 0;
        if (!TrashStore.BuildTrashPath(trash, ref tl)) { Vga.WriteLine("No home directory"); return; }
        uint trashIno;
        if (!Ext4.Resolve(trash, tl, out trashIno)) { Vga.WriteLine("(empty)"); return; }
        byte* din = stackalloc byte[256];
        if (!Ext4.ReadInode(trashIno, din) || !Ext4.IsDir(Ext4.InodeGetMode(din)))
        {
            Vga.WriteLine("(empty)");
            return;
        }
        if (!Osb.Kernel.Shell.Users.CanRead(Ext4.InodeGetUid(din), Ext4.InodeGetMode(din)))
        {
            Vga.WriteLine("Permission denied");
            return;
        }

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
                    char* nm = stackalloc char[256];
                    for (int k = 0; k < nlen; k++) nm[k] = (char)de[Layout.DirName + k];
                    bool hidden = (nm[0] == '.');          // ".", "..", the index
                    if (!hidden && Match(nm, nlen, pattern, patLen))
                    {
                        byte* cin = stackalloc byte[256];
                        ulong sz = 0;
                        if (Ext4.ReadInode(iino, cin)) sz = Ext4.InodeGetSize(cin);
                        any = true;
                        Vga.WriteStr(nm, nlen);
                        Vga.Write("  ");
                        Vga.WriteDec(sz);
                        byte* orig = stackalloc byte[PathCap];
                        int olen = TrashStore.FindOrigin(trashIno, nm, nlen, orig, PathCap);
                        if (olen > 0)
                        {
                            Vga.Write("  was: ");
                            for (int k = 0; k < olen; k++) Vga.PutChar((char)orig[k]);
                        }
                        Vga.PutChar('\n');
                    }
                }
                off += rlen;
                if (off > Ext4.BlockSize - 8) break;
            }
        }
        if (!any) Vga.WriteLine("(empty)");
    }

    // Restores <name> from the trash to its original folder
    // (or the current directory when that folder is gone).
    private static unsafe void Undelete(char* name, int nlen)
    {
        if (!Ext4.Mount()) { Vga.WriteLine("No filesystem"); return; }
        char* trash = stackalloc char[PathCap];
        int tl = 0;
        if (!TrashStore.BuildTrashPath(trash, ref tl)) { Vga.WriteLine("No home directory"); return; }
        uint trashIno;
        if (!Ext4.Resolve(trash, tl, out trashIno)) { Vga.WriteLine("Trash is empty"); return; }

        uint childIno; byte childType;
        if (!Ext4.DirEntryExists(trashIno, name, nlen, out childIno, out childType))
        {
            Vga.WriteLine("Not in trash");
            return;
        }

        // original location, recorded when the file was deleted
        byte* orig = stackalloc byte[PathCap];
        int olen = TrashStore.FindOrigin(trashIno, name, nlen, orig, PathCap);

        char* parentPath = stackalloc char[PathCap];
        int plen = 0;
        char* baseName = stackalloc char[256];
        int blen = 0;
        bool haveOrig = false;
        if (olen > 0)
        {
            int last = -1;
            for (int k = olen - 1; k >= 0; k--)
                if (orig[k] == '\\' || orig[k] == '/') { last = k; break; }
            if (last > 0)
            {
                plen = last;
                for (int k = 0; k < plen; k++) parentPath[k] = (char)orig[k];
                blen = olen - last - 1;
                for (int k = 0; k < blen; k++) baseName[k] = (char)orig[last + 1 + k];
                haveOrig = true;
            }
        }

        uint parentIno = 0;
        if (haveOrig)
        {
            if (!Ext4.Resolve(parentPath, plen, out parentIno))
                haveOrig = false;                 // original folder is gone
        }
        if (!haveOrig)
        {
            if (!Ext4.GetCurrentDirIno(out parentIno)) { Vga.WriteLine("Error"); return; }
            blen = nlen;
            for (int k = 0; k < blen; k++) baseName[k] = name[k];
        }

        if (!Writable(parentIno))
        {
            Vga.WriteLine("Permission denied");
            return;
        }

        // never clobber an existing file in the destination
        char* finalName = stackalloc char[256];
        int flen = 0;
        if (!TrashStore.PickFreeName(parentIno, baseName, blen, finalName, ref flen))
        {
            Vga.WriteLine("No free name");
            return;
        }

        if (!Ext4.DirAddEntry(parentIno, finalName, flen, childIno, childType))
        {
            Vga.WriteLine("Error restoring");
            return;
        }
        if (!Ext4.DirRemoveEntry(trashIno, name, nlen))
        {
            Ext4.DirRemoveEntry(parentIno, finalName, flen);   // rollback
            Vga.WriteLine("Error restoring");
            return;
        }
        TrashStore.RemoveOrigin(trashIno, name, nlen);
        Vga.WriteLine("OK");
    }

    // Case-insensitive substring match (an empty pattern matches all).
    private static unsafe bool Match(char* name, int nlen, char* pat, int patLen)
    {
        if (patLen <= 0) return true;
        if (patLen > nlen) return false;
        for (int i = 0; i + patLen <= nlen; i++)
        {
            bool ok = true;
            for (int k = 0; k < patLen; k++)
            {
                char a = name[i + k];
                char b = pat[k];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (b >= 'a' && b <= 'z') b -= (char)32;
                if (a != b) { ok = false; break; }
            }
            if (ok) return true;
        }
        return false;
    }

    // Write permission on a directory (the unix rule for its entries).
    private static unsafe bool Writable(uint dirIno)
    {
        byte* pin = stackalloc byte[256];
        if (!Ext4.ReadInode(dirIno, pin)) return false;
        return Osb.Kernel.Shell.Users.CanWrite(Ext4.InodeGetUid(pin), Ext4.InodeGetMode(pin));
    }
}
