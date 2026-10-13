// Simplified ext4 reader/writer (as formatted by build.sh): 4096-byte blocks,
// no journal, no extents, no flex_bg, no metadata_csm, case-sensitive.
// One group; primary superblock at byte 1024 of block 0, GDT at block 1.
//
// The layer exposes: block/inode I/O, inode field accessors and path
// resolution (drive + current directory aware). It knows nothing about the
// shell - callers pass a path and get an inode back.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Storage;

internal static unsafe class Ext4
{
    public const int BlockSize = Layout.Ext4BlockSize;   // 4096
    public const uint RootInode = 2;

    // ---- superblock (sb points at block-bytes + 1024) ----
    private static unsafe ushort SbMagic(byte* sb) { return Rd16(sb, 56); }
    private static unsafe ushort SbInodeSize(byte* sb) { ushort r = Rd16(sb, 88); return r >= 128 ? r : (ushort)128; }
    private static unsafe uint SbInodesPerGroup(byte* sb) { return Rd32(sb, 40); }
    private static unsafe uint SbBlocksPerGroup(byte* sb) { return Rd32(sb, 32); }
    private static unsafe uint SbBlocksCount(byte* sb) { return Rd32(sb, 4); }

    // ---- group descriptor entry (32 bytes each) ----
    private static unsafe uint GdBlockBitmap(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32); }
    private static unsafe uint GdInodeBitmap(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32 + 4); }
    private static unsafe uint GdInodeTable(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32 + 8); }

    // ---- mounted state (filled by Mount) ----
    private static bool _mounted;
    private static int _inodeSize = 256;
    private static uint _inodesPerGroup, _blocksPerGroup, _groupCount;
    private static uint _blkBitmapBlock, _inoBitmapBlock, _inoTableBlock;

    public static bool Mount()
    {
        if (_mounted) return true;
        byte* sbuf = stackalloc byte[BlockSize];
        ReadBlock(0UL, sbuf);
        byte* sb = sbuf + 1024;
        if (SbMagic(sb) != 0xEF53) return false;
        _inodeSize = SbInodeSize(sb);
        _inodesPerGroup = SbInodesPerGroup(sb);
        _blocksPerGroup = SbBlocksPerGroup(sb);
        uint blocks = SbBlocksCount(sb);
        _groupCount = (blocks + _blocksPerGroup - 1) / _blocksPerGroup;
        if (_groupCount == 0) _groupCount = 1;
        byte* gdt = stackalloc byte[BlockSize];
        ReadBlock(1UL, gdt);
        _blkBitmapBlock = GdBlockBitmap(gdt, 0);
        _inoBitmapBlock = GdInodeBitmap(gdt, 0);
        _inoTableBlock = GdInodeTable(gdt, 0);
        _mounted = true;
        return true;
    }

    // ---- block I/O (one block = eight 512-byte sectors) ----
    public static void ReadBlock(ulong block, byte* buf)
    {
        ulong lba = block * 8UL;
        for (int i = 0; i < 8; i++) Ata.ReadSector(lba + (ulong)i, buf + (i * 512));
    }

    public static void WriteBlock(ulong block, byte* buf)
    {
        ulong lba = block * 8UL;
        for (int i = 0; i < 8; i++) Ata.WriteSector(lba + (ulong)i, buf + (i * 512));
    }

    // ---- inode table (contiguous blocks; an inode spans at most 2 blocks) ----
    public static bool ReadInode(uint ino, byte* outBuf)
    {
        if (!_mounted || ino == 0) return false;
        ulong abs = (ulong)_inoTableBlock * (ulong)BlockSize + (ulong)(ino - 1) * (ulong)_inodeSize;
        byte* blk0 = stackalloc byte[BlockSize];
        ReadBlock(abs / (ulong)BlockSize, blk0);
        ulong off0 = abs % (ulong)BlockSize;
        ulong first = (ulong)BlockSize - off0;
        if (first > (ulong)_inodeSize) first = (ulong)_inodeSize;
        for (ulong i = 0; i < first; i++) outBuf[i] = blk0[off0 + i];
        if (first < (ulong)_inodeSize)
        {
            byte* blk1 = stackalloc byte[BlockSize];
            ReadBlock((abs + first) / (ulong)BlockSize, blk1);
            for (ulong i = first; i < (ulong)_inodeSize; i++) outBuf[i] = blk1[(off0 + i) - (ulong)BlockSize];
        }
        return true;
    }

    public static bool WriteInode(uint ino, byte* inBuf)
    {
        if (!_mounted || ino == 0) return false;
        ulong abs = (ulong)_inoTableBlock * (ulong)BlockSize + (ulong)(ino - 1) * (ulong)_inodeSize;
        byte* blk0 = stackalloc byte[BlockSize];
        ReadBlock(abs / (ulong)BlockSize, blk0);
        ulong off0 = abs % (ulong)BlockSize;
        ulong first = (ulong)BlockSize - off0;
        if (first > (ulong)_inodeSize) first = (ulong)_inodeSize;
        for (ulong i = 0; i < first; i++) blk0[off0 + i] = inBuf[i];
        WriteBlock(abs / (ulong)BlockSize, blk0);
        if (first < (ulong)_inodeSize)
        {
            byte* blk1 = stackalloc byte[BlockSize];
            ReadBlock((abs + first) / (ulong)BlockSize, blk1);
            for (ulong i = first; i < (ulong)_inodeSize; i++) blk1[(off0 + i) - (ulong)BlockSize] = inBuf[i];
            WriteBlock((abs + first) / (ulong)BlockSize, blk1);
        }
        return true;
    }

    // ---- inode fields ----
    public static unsafe ushort InodeGetMode(byte* n) { return Rd16(n, Layout.InoMode); }
    public static unsafe uint InodeGetUid(byte* n) { return Rd16(n, Layout.InoUid); }
    public static unsafe uint InodeGetGid(byte* n) { return Rd16(n, Layout.InoGid); }
    public static unsafe uint InodeGetLinks(byte* n) { return Rd16(n, Layout.InoLinks); }
    public static unsafe uint InodeGetMtime(byte* n) { return Rd32(n, Layout.InoMtime); }

    public static unsafe uint InodeGetSize(byte* n)
    {
        ulong hi = Rd32(n, Layout.InoSizeHi);
        ulong lo = Rd32(n, Layout.InoSizeLo);
        ulong sz = (hi << 32) | lo;
        return sz > 0xFFFFFFFFUL ? 0xFFFFFFFFU : (uint)sz;
    }

    public static unsafe uint InodeGetBlocks(byte* n)
    {
        ulong lo = Rd32(n, Layout.InoBlocksLo);
        ulong hi = (ulong)Rd16(n, Layout.InoBlocksHi);
        ulong b = lo | (hi << 32);
        return b > 0xFFFFFFFFUL ? 0xFFFFFFFFU : (uint)b;
    }

    public static bool IsDir(ushort mode) { return (mode & 0xF000) == 0x4000; }
    public static bool IsReg(ushort mode) { return (mode & 0xF000) == 0x8000; }

    // Logical -> physical block: 12 direct + single indirect.
    public static unsafe uint InodeGetBlock(byte* inode, ulong logical)
    {
        if (logical < 12) return InodeBlockPtr(inode, (int)logical);
        if (logical < 12 + 1024)
        {
            uint ib = InodeBlockPtr(inode, 12);
            if (ib == 0) return 0;
            byte* ibuf = stackalloc byte[BlockSize];
            ReadBlock(ib, ibuf);
            return Rd32(ibuf, (int)(logical - 12) * 4);
        }
        return 0; // double indirect: not needed for the OSB dev image
    }

    private static unsafe uint InodeBlockPtr(byte* n, int idx) { return (idx < 0 || idx > 14) ? 0U : Rd32(n, Layout.InoBlock + idx * 4); }

    // ---- ext4 directory entries (ext4_dir_entry_2) ----
    public static unsafe uint DirEntryInode(byte* de) { return Rd32(de, Layout.DirIno); }
    public static unsafe ushort DirEntryRecLen(byte* de) { return Rd16(de, Layout.DirRecLen); }
    public static unsafe byte DirEntryNameLen(byte* de) { return de[Layout.DirNameLen]; }
    public static unsafe byte DirEntryType(byte* de) { return de[Layout.DirType]; }

    // Case-sensitive child lookup: name must match exactly.
    private static unsafe bool FindInDir(byte* dirIno, byte* name, int nameLen, out uint childIno, out byte childType)
    {
        childIno = 0;
        childType = 0;
        if (!IsDir(InodeGetMode(dirIno))) return false;
        uint size = InodeGetSize(dirIno);
        ulong blocks = ((ulong)size + (ulong)BlockSize - 1) / (ulong)BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[BlockSize];
        for (ulong b = 0; b < blocks; b++)
        {
            uint bnum = InodeGetBlock(dirIno, b);
            if (bnum == 0) continue;
            ReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= BlockSize)
            {
                byte* de = blk + off;
                uint iino = DirEntryInode(de);
                ushort rlen = DirEntryRecLen(de);
                if (rlen == 0) break;
                byte nlen = DirEntryNameLen(de);
                if (iino != 0 && nlen == (byte)nameLen)
                {
                    bool same = true;
                    for (int k = 0; k < nameLen; k++)
                        if (de[Layout.DirName + k] != name[k]) { same = false; break; }
                    if (same) { childIno = iino; childType = DirEntryType(de); return true; }
                }
                off += rlen;
                if (off > BlockSize - 8) break;
            }
        }
        return false;
    }

    // Resolves "A:\OSB\BIN", "\OSB", "OSB", "A:OSB", "." and ".." using the
    // current directory of drive A:. Returns the inode and builds the
    // canonical absolute path ("A:\OSB\BIN") in canon.
    public static unsafe bool ResolveCanonical(char* path, int len, out uint ino, byte* canon, ref int canonLen)
    {
        ino = 0;
        if (!_mounted && !Mount()) return false;
        byte* abs = stackalloc byte[300];
        int al = 0;
        int i = 0;
        bool absolute = false;
        // drive prefix
        if (len >= 2 && path[1] == ':')
        {
            char d = path[0];
            if (d >= 'a' && d <= 'z') d = (char)(d - 32);
            if (d != 'A') { Vga.WriteLine("Drive not ready."); return false; }
            i = 2;
            while (i < len && (path[i] == '\\' || path[i] == '/')) { i++; absolute = true; }
        }
        if (!absolute && len >= 1 && (path[0] == '\\' || path[0] == '/'))
        {
            i = 1;
            absolute = true;
        }
        if (absolute)
        {
            abs[0] = (byte)'A'; abs[1] = (byte)':'; abs[2] = (byte)'\\'; al = 3;
        }
        else
        {
            byte* cwd = stackalloc byte[160];
            int cl = Cwd.CopyTo(cwd, 160);
            for (int k = 0; k < cl && al < 299; k++) abs[al++] = cwd[k];
        }
        // append the input segments
        while (i < len)
        {
            while (i < len && (path[i] == '\\' || path[i] == '/' || path[i] == ' ')) i++;
            int s = i;
            while (i < len && path[i] != '\\' && path[i] != '/') i++;
            int segLen = i - s;
            if (segLen > 0)
            {
                if (al > 0 && abs[al - 1] != '\\') abs[al++] = (byte)'\\';
                if (al + segLen > 299) return false;
                for (int k = 0; k < segLen; k++) abs[al++] = (byte)path[s + k];
            }
        }

        // walk the segments from the root
        byte* din = stackalloc byte[256];
        ino = RootInode;
        uint clen = 0;
        canon[clen++] = (byte)'A'; canon[clen++] = (byte)':'; canon[clen++] = (byte)'\\';
        int pos = 3;
        while (pos < al)
        {
            while (pos < al && (abs[pos] == (byte)'\\' || abs[pos] == (byte)'/')) pos++;
            int s = pos;
            while (pos < al && abs[pos] != (byte)'\\' && abs[pos] != (byte)'/') pos++;
            int segLen = pos - s;
            if (segLen <= 0) continue;
            if (segLen == 1 && abs[s] == (byte)'.') continue;
            if (segLen == 2 && abs[s] == (byte)'.' && abs[s + 1] == (byte)'.')
            {
                // parent: shrink canon to the previous separator
                if (clen > 3)
                {
                    clen--;
                    while (clen > 3 && canon[clen - 1] != (byte)'\\') clen--;
                }
                // resolve parent inode via the ".." entry
                if (!ReadInode(ino, din)) return false;
                byte* dd = stackalloc byte[2];
                dd[0] = (byte)'.'; dd[1] = (byte)'.';
                uint pino; byte pt;
                if (!FindInDir(din, dd, 2, out pino, out pt)) return false;
                ino = pino;
                continue;
            }
            // normal segment: look it up in the current directory
            if (!ReadInode(ino, din)) return false;
            uint cino;
            byte ct;
            if (!FindInDir(din, abs + s, segLen, out cino, out ct)) return false;
            ino = cino;
            if (clen > 3 && canon[clen - 1] != (byte)'\\') { if (clen >= 299) return false; canon[clen++] = (byte)'\\'; }
            for (int k = 0; k < segLen; k++) { if (clen >= 299) return false; canon[clen++] = abs[s + k]; }
        }
        canon[clen] = 0;
        canonLen = (int)clen;
        return true;
    }

    // Path -> inode; relative paths are resolved against the current directory.
    public static bool Resolve(char* path, int len, out uint ino)
    {
        byte* canon = stackalloc byte[300];
        int canonLen = 0;
        return ResolveCanonical(path, len, out ino, canon, ref canonLen);
    }

    // ---- little-endian 16/32-bit accessors ----
    private static ushort Rd16(byte* p, int o) { return (ushort)(p[o] | (p[o + 1] << 8)); }
    private static uint Rd32(byte* p, int o) { return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)); }
    private static void Wr16(byte* p, int o, ushort v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)(v >> 8); }
    private static void Wr32(byte* p, int o, uint v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)((v >> 8) & 0xFF); p[o + 2] = (byte)((v >> 16) & 0xFF); p[o + 3] = (byte)((v >> 24) & 0xFF); }
}