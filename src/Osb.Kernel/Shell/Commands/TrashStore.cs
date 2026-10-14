// Trash storage internals: $HOME resolution, the per-user
// trash folder and the ".ORIG" index that remembers where a
// trashed file came from. Plain ext4 directories and files;
// used by TrashCommands.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class TrashStore
{
    // Index file mapping a trashed name to its original path.
    internal const string IndexName = ".ORIG";

    // Scratch for the index file. RecordOrigin and
    // RemoveOrigin call Ext4.WriteFile, whose block-
    // allocating paths already run deep in the stack
    // (4 KiB+ each), so the index content lives here
    // instead of in stackallocs: the 16 KiB task
    // stack would overflow.
    private const int IndexCap = 4200;
    private unsafe struct IndexBuf { public fixed byte data[IndexCap]; }
    private static IndexBuf _indexRead;
    private static IndexBuf _indexWrite;

    // $HOME (from the environment), falling back to the
    // session user's home folder. False when neither is known.
    internal static unsafe bool HomePath(char* dst, ref int dlen)
    {
        fixed (char* nm = "HOME")
        {
            int vo; int vl;
            if (Osb.Kernel.Shell.Environment.TryGet(nm, 4, out vo, out vl) && vl > 0)
            {
                dlen = vl < 256 ? vl : 255;
                for (int k = 0; k < dlen; k++) dst[k] = Osb.Kernel.Shell.Environment.ValueAt(vo + k);
                return true;
            }
        }
        if (Osb.Kernel.Shell.Users.LoggedIn)
        {
            dlen = Osb.Kernel.Shell.Users.HomeAt(Osb.Kernel.Shell.Users.CurrentIndex, dst);
            return dlen > 0;
        }
        return false;
    }

    // Builds the current user's $HOME\TRASH path.
    internal static unsafe bool BuildTrashPath(char* dst, ref int dlen)
    {
        char* home = stackalloc char[256];
        int hl = 0;
        if (!HomePath(home, ref hl)) return false;
        AppendPath(dst, ref dlen, home, hl);
        AppendLit(dst, ref dlen, "\\TRASH");
        return true;
    }

    // Creates $HOME and $HOME\TRASH for the session user when missing.
    internal static unsafe bool EnsureTrash()
    {
        char* home = stackalloc char[256];
        int hl = 0;
        if (!HomePath(home, ref hl)) return false;
        uint hino;
        if (!Ext4.Resolve(home, hl, out hino))
        {
            if (!FileCommands.MakeDir(home, hl, (ushort)(0x4000 | 0x1ED),
                (uint)Osb.Kernel.Shell.Users.Uid, out _)) return false;
        }
        char* trash = stackalloc char[300];
        int tl = 0;
        AppendPath(trash, ref tl, home, hl);
        AppendLit(trash, ref tl, "\\TRASH");
        uint tino;
        if (Ext4.Resolve(trash, tl, out tino)) return true;
        return FileCommands.MakeDir(trash, tl, (ushort)(0x4000 | 0x1ED),
            (uint)Osb.Kernel.Shell.Users.Uid, out _);
    }

    // Appends "name<TAB>path\n" to the trash index file.
    internal static unsafe void RecordOrigin(uint trashIno, char* name, int nlen, byte* canon, int canonLen)
    {
        fixed (char* iname = IndexName)
        {
            uint ino; byte type;
            if (!Ext4.DirEntryExists(trashIno, iname, IndexName.Length, out ino, out type))
            {
                if (!Ext4.AllocInode(out ino, 0x81A4)) return;
                if (!Ext4.DirAddEntry(trashIno, iname, IndexName.Length, ino, 1))
                {
                    Ext4.FreeInode(ino);
                    return;
                }
            }
            fixed (byte* buf = _indexWrite.data)
            {
            int got = 0;
            Ext4.ReadFileAll(ino, buf, IndexCap, out got);
            if (got + nlen + canonLen + 2 > IndexCap) return;   // index full: /U falls back to cwd
            for (int k = 0; k < nlen; k++) buf[got++] = (byte)name[k];
            buf[got++] = (byte)'\t';
            for (int k = 0; k < canonLen; k++) buf[got++] = canon[k];
            buf[got++] = (byte)'\n';
                Ext4.WriteFile(ino, buf, got);
            }
        }
    }

    // Looks up the original path of `name` in the index; copies
    // it into dst and returns its length (-1 when unknown).
    internal static unsafe int FindOrigin(uint trashIno, char* name, int nlen, byte* dst, int cap)
    {
        fixed (char* iname = IndexName)
        {
            uint ino; byte type;
            if (!Ext4.DirEntryExists(trashIno, iname, IndexName.Length, out ino, out type)) return -1;
            fixed (byte* buf = _indexRead.data)
            {
            int got = 0;
            if (!Ext4.ReadFileAll(ino, buf, IndexCap, out got) || got <= 0) return -1;
            int pos = 0;
            while (pos < got)
            {
                int eol = pos;
                while (eol < got && buf[eol] != '\n') eol++;
                int tab = pos;
                while (tab < eol && buf[tab] != '\t') tab++;
                if (tab - pos == nlen)
                {
                    bool same = true;
                    for (int k = 0; k < nlen; k++)
                        if (buf[pos + k] != (byte)name[k]) { same = false; break; }
                    if (same)
                    {
                        int pathLen = eol - tab - 1;
                        if (pathLen <= 0) return -1;
                        if (pathLen > cap - 1) pathLen = cap - 1;
                        for (int k = 0; k < pathLen; k++) dst[k] = buf[tab + 1 + k];
                        return pathLen;
                    }
                }
                pos = eol + 1;
            }
            return -1;
            }
        }
    }

    // Drops the index line for `name` (rewrites the index file).
    internal static unsafe void RemoveOrigin(uint trashIno, char* name, int nlen)
    {
        fixed (char* iname = IndexName)
        {
            uint ino; byte type;
            if (!Ext4.DirEntryExists(trashIno, iname, IndexName.Length, out ino, out type)) return;
            fixed (byte* buf = _indexRead.data)
            fixed (byte* outBuf = _indexWrite.data)
            {
            int got = 0;
            if (!Ext4.ReadFileAll(ino, buf, IndexCap, out got) || got <= 0) return;
            int olen = 0;
            int pos = 0;
            while (pos < got)
            {
                int eol = pos;
                while (eol < got && buf[eol] != '\n') eol++;
                int tab = pos;
                while (tab < eol && buf[tab] != '\t') tab++;
                bool drop = (tab - pos == nlen);
                if (drop)
                    for (int k = 0; k < nlen; k++)
                        if (buf[pos + k] != (byte)name[k]) { drop = false; break; }
                if (!drop && olen + (eol - pos) + 1 <= IndexCap)
                {
                    for (int k = pos; k < eol; k++) outBuf[olen++] = buf[k];
                    outBuf[olen++] = (byte)'\n';
                }
                pos = eol + 1;
            }
            Ext4.WriteFile(ino, outBuf, olen);
            }
        }
    }

    // A free name in `dirIno`: the requested one, else name~1, name~2...
    internal static unsafe bool PickFreeName(uint dirIno, char* baseName, int baseLen, char* dst, ref int dlen)
    {
        if (baseLen > 250) return false;
        uint ex; byte et;
        if (!Ext4.DirEntryExists(dirIno, baseName, baseLen, out ex, out et))
        {
            dlen = baseLen;
            for (int k = 0; k < baseLen; k++) dst[k] = baseName[k];
            return true;
        }
        for (int n = 1; n < 100; n++)
        {
            int nl = baseLen;
            for (int k = 0; k < baseLen; k++) dst[k] = baseName[k];
            dst[nl++] = '~';
            int t = n;
            char* tmp = stackalloc char[4];
            int tc = 0;
            while (t > 0 && tc < 4) { tmp[tc++] = (char)('0' + t % 10); t /= 10; }
            for (int k = tc - 1; k >= 0; k--) dst[nl++] = tmp[k];
            if (!Ext4.DirEntryExists(dirIno, dst, nl, out ex, out et))
            {
                dlen = nl;
                return true;
            }
        }
        return false;
    }

    internal static unsafe void AppendPath(char* dst, ref int len, char* src, int slen)
    {
        for (int k = 0; k < slen; k++) dst[len++] = src[k];
    }

    internal static unsafe void AppendLit(char* dst, ref int len, string lit)
    {
        fixed (char* p = lit)
        {
            for (int k = 0; k < lit.Length; k++) dst[len++] = p[k];
        }
    }
}
