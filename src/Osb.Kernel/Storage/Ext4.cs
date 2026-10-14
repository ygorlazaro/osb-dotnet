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

    public static unsafe bool IsExtentInode(byte* inode)
    {
        if (inode == null) return false;
        uint flags = Rd32(inode, Layout.InoFlags);
        return (flags & 0x00080000U) != 0; // EXT4_EXTENTS_FL
    }

    private static unsafe uint ExtentBlockAt(byte* inode, ulong logical)
    {
        // extent header at inode i_block (offset Layout.InoBlock)
        byte* eh = inode + Layout.InoBlock;
        if (Rd16(eh, 0) != 0xF30A) return 0; // magic
        ushort entries = Rd16(eh, 2);
        ushort depth = Rd16(eh, 6);
        if (depth == 0)
        {
            for (int i = 0; i < entries; i++)
            {
                byte* ext = eh + 12 + (i * 12);
                uint first = Rd32(ext, 0);
                ushort len = (ushort)(Rd16(ext, 4) & 0x7FFF);
                if (len == 0) continue;
                if (logical < first) continue;
                if (logical >= first + len) continue;
                uint hi = Rd16(ext, 6);
                uint lo = Rd32(ext, 8);
                ulong phys = ((ulong)hi << 32) | (ulong)lo;
                if (phys == 0) return 0;
                return (uint)(phys + (logical - first));
            }
            return 0;
        }
        // internal node: find the index covering logical
        int chosen = -1;
        for (int i = 0; i < entries; i++)
        {
            byte* idx = eh + 12 + (i * 12);
            uint blk = Rd32(idx, 0);
            if (blk <= logical) chosen = i; else break;
        }
        if (chosen < 0) return 0;
        byte* idxe = eh + 12 + (chosen * 12);
        uint leafHi = Rd16(idxe, 10);
        uint leafLo = Rd32(idxe, 4);
        ulong leafBlk = ((ulong)leafHi << 32) | (ulong)leafLo;
        if (leafBlk == 0) return 0;
        // read leaf node
        byte* buf = stackalloc byte[BlockSize];
        ReadBlock(leafBlk, buf);
        byte* leh = buf;
        if (Rd16(leh, 0) != 0xF30A) return 0;
        ushort lentries = Rd16(leh, 2);
        ushort ldepth = Rd16(leh, 6);
        // assume leaf depth 0 after one level
        if (ldepth != 0) return 0;
        for (int i = 0; i < lentries; i++)
        {
            byte* ext = leh + 12 + (i * 12);
            uint first = Rd32(ext, 0);
            ushort len = (ushort)(Rd16(ext, 4) & 0x7FFF);
            if (len == 0) continue;
            if (logical < first) continue;
            if (logical >= first + len) continue;
            uint hi = Rd16(ext, 6);
            uint lo = Rd32(ext, 8);
            ulong phys = ((ulong)hi << 32) | (ulong)lo;
            if (phys == 0) return 0;
            return (uint)(phys + (logical - first));
        }
        return 0;
    }

    // Logical -> physical block, extent- or legacy-based.
    public static unsafe uint InodeGetBlock(byte* inode, ulong logical)
    {
        if (IsExtentInode(inode)) return ExtentBlockAt(inode, logical);

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

    // ---- filesystem mutation helpers (for self-contained shell file commands) ----

    public static unsafe bool GetCurrentDirIno(out uint ino)
    {
        ino = 0;
        if (!_mounted && !Mount()) return false;
        byte* cb = stackalloc byte[160];
        int cl = Cwd.CopyTo(cb, 160);
        char* cc = stackalloc char[160];
        for (int k = 0; k < cl && k < 160; k++) cc[k] = (char)cb[k];
        return Resolve(cc, cl, out ino);
    }

    // Keeps the free-blocks/free-inodes summary counters (superblock + group
    // descriptor) in sync with the bitmaps, so the fs stays e2fsck-clean.
    // Uses one static block buffer: the fs layer already runs deep in the
    // stack with its 4 KiB scratch buffers, and these paths must not grow it.
    private unsafe struct MetaBlock { public fixed byte data[4096]; }
    private static MetaBlock _meta;

    private static void AdjustFreeCounts(int blockDelta, int inodeDelta, int dirDelta)
    {
        fixed (byte* p = _meta.data)
        {
            // superblock sits at byte 1024 of block 0:
            // s_free_blocks_count_lo at +12, s_free_inodes_count at +16
            ReadBlock(0UL, p);
            Wr32(p + 1024 + 12, 0, (uint)((int)Rd32(p + 1024 + 12, 0) + blockDelta));
            Wr32(p + 1024 + 16, 0, (uint)((int)Rd32(p + 1024 + 16, 0) + inodeDelta));
            WriteBlock(0UL, p);

            // group descriptor 0 at block 1:
            // bg_free_blocks_count +12, bg_free_inodes_count +14, bg_used_dirs_count +16
            ReadBlock(1UL, p);
            Wr16(p, 12, (ushort)((int)Rd16(p, 12) + blockDelta));
            Wr16(p, 14, (ushort)((int)Rd16(p, 14) + inodeDelta));
            Wr16(p, 16, (ushort)((int)Rd16(p, 16) + dirDelta));
            WriteBlock(1UL, p);
        }
    }

    public static unsafe bool AllocBlock(out uint block)
    {
        block = 0;
        if (!_mounted && !Mount()) return false;
        byte* bm = stackalloc byte[BlockSize];
        ReadBlock(_blkBitmapBlock, bm);
        for (uint i = 0; i < _blocksPerGroup; i++)
        {
            if ((bm[i >> 3] & (byte)(1 << (int)(i & 7))) == 0)
            {
                bm[i >> 3] = (byte)(bm[i >> 3] | (byte)(1 << (int)(i & 7)));
                WriteBlock(_blkBitmapBlock, bm);
                byte* zb = stackalloc byte[BlockSize];
                for (int k = 0; k < BlockSize; k++) zb[k] = 0;
                WriteBlock(i, zb);
                block = i;
                AdjustFreeCounts(-1, 0, 0);
                return true;
            }
        }
        return false;
    }

    public static unsafe bool FreeBlock(uint block)
    {
        if (!_mounted && !Mount()) return false;
        if (block == 0) return true;
        byte* bm = stackalloc byte[BlockSize];
        ReadBlock(_blkBitmapBlock, bm);
        bm[block >> 3] = (byte)(bm[block >> 3] & ~(byte)(1 << (int)(block & 7)));
        WriteBlock(_blkBitmapBlock, bm);
        AdjustFreeCounts(1, 0, 0);
        return true;
    }

    public static unsafe bool AllocInode(out uint ino, ushort mode)
    {
        ino = 0;
        if (!_mounted && !Mount()) return false;
        byte* bm = stackalloc byte[BlockSize];
        ReadBlock(_inoBitmapBlock, bm);
        for (uint i = 1; i < _inodesPerGroup; i++)
        {
            // the inode bitmap is 1-based: inode N lives at bit N-1
            int bit = (int)(i - 1);
            if ((bm[bit >> 3] & (byte)(1 << (bit & 7))) == 0)
            {
                bm[bit >> 3] = (byte)(bm[bit >> 3] | (byte)(1 << (bit & 7)));
                WriteBlock(_inoBitmapBlock, bm);
                ino = i;
                byte* inBuf = stackalloc byte[256];
                for (int k = 0; k < 256; k++) inBuf[k] = 0;
                Wr16(inBuf, Layout.InoMode, mode);
                Wr16(inBuf, Layout.InoLinks, (mode & 0xF000) == 0x4000 ? (ushort)2 : (ushort)1);
                Wr16(inBuf, Layout.InoUid, 0);
                Wr16(inBuf, Layout.InoGid, 0);
                Wr32(inBuf, Layout.InoSizeLo, 0);
                Wr32(inBuf, Layout.InoSizeHi, 0);
                Wr32(inBuf, Layout.InoBlocksLo, 0);
                Wr16(inBuf, Layout.InoBlocksHi, 0);
                WriteInode(ino, inBuf);
                AdjustFreeCounts(0, -1, (mode & 0xF000) == 0x4000 ? 1 : 0);
                return true;
            }
        }
        return false;
    }

    public static unsafe bool FreeInode(uint ino)
    {
        if (ino == 0) return true;
        if (!_mounted && !Mount()) return false;
        // Free the data blocks first (direct pointers + the single indirect
        // block): a slow symlink's target lives in the block at i_block[0],
        // and a regular file in its own blocks. Without this, deleting a
        // slow symlink leaks its target block for the life of the fs.
        byte* inBuf = stackalloc byte[256];
        bool wasDir = false;
        if (ReadInode(ino, inBuf))
        {
            wasDir = IsDir(InodeGetMode(inBuf));
            uint sz = InodeGetSize(inBuf);
            uint blocks = (sz + (uint)BlockSize - 1) / (uint)BlockSize;
            if (blocks == 0 && sz > 0) blocks = 1;
            if (blocks == 0 && IsSymlink(inBuf))
            {
                // slow symlink: size == target length, one block at i_block[0]
                if (Rd32(inBuf, Layout.InoBlock) != 0) blocks = 1;
            }
            for (uint lb = 0; lb < blocks; lb++)
            {
                uint pb = InodeGetBlock(inBuf, lb);
                if (pb != 0) FreeBlock(pb);
            }
            if (blocks > 12)
            {
                uint ib = Rd32(inBuf, Layout.InoBlock + 12 * 4);
                if (ib != 0) FreeBlock(ib);
            }
        }
        byte* bm = stackalloc byte[BlockSize];
        ReadBlock(_inoBitmapBlock, bm);
        int bit = (int)(ino - 1);   // the inode bitmap is 1-based: inode N lives at bit N-1
        bm[bit >> 3] = (byte)(bm[bit >> 3] & ~(byte)(1 << (bit & 7)));
        WriteBlock(_inoBitmapBlock, bm);
        AdjustFreeCounts(0, 1, wasDir ? -1 : 0);
        for (int k = 0; k < 256; k++) inBuf[k] = 0;
        WriteInode(ino, inBuf);
        return true;
    }

    private static unsafe void SetInodeSize(byte* ino, uint size)
    {
        Wr32(ino, Layout.InoSizeLo, size);
        Wr32(ino, Layout.InoSizeHi, 0);
    }

    private static unsafe void SetInodeBlocks(byte* ino, uint blocks512)
    {
        Wr32(ino, Layout.InoBlocksLo, blocks512);
        // i_blocks_hi: blocks512 is a uint, so the high word needs a 64-bit
        // shift (a uint >> 32 would shift by 32 % 32 == 0)
        Wr16(ino, Layout.InoBlocksHi, (ushort)((ulong)blocks512 >> 32));
    }

    private static unsafe uint GetBlockPtr(byte* ino, int idx)
    {
        return Rd32(ino, Layout.InoBlock + idx * 4);
    }

    private static unsafe void SetBlockPtr(byte* ino, int idx, uint block)
    {
        Wr32(ino, Layout.InoBlock + idx * 4, block);
    }

    public static unsafe bool WriteFile(uint ino, byte* data, int len)
    {
        if (len < 0) len = 0;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(ino, inode)) return false;
        uint iflags = Rd32(inode, Layout.InoFlags);
        bool wasExtent = (iflags & 0x00080000U) != 0;
        // free old blocks
        uint oldSize = InodeGetSize(inode);
        uint oldBlocks = (oldSize + (uint)BlockSize - 1) / (uint)BlockSize;
        if (oldBlocks == 0 && oldSize > 0) oldBlocks = 1;
        for (uint lb = 0; lb < oldBlocks; lb++)
        {
            uint pb = InodeGetBlock(inode, lb);
            if (pb != 0) FreeBlock(pb);
        }
        if (oldBlocks > 12)
        {
            uint ib = GetBlockPtr(inode, 12);
            if (ib != 0) FreeBlock(ib);
        }
        for (int k = Layout.InoBlock; k < Layout.InoBlock + 15 * 4; k += 4) Wr32(inode, k, 0);
        // the rewrite below uses legacy direct pointers: an extent-mapped inode
        // (like the seeded PASSWD/SUDOERS) must lose the extents flag, or reads
        // keep parsing i_block as an extent header and find no data
        if (wasExtent) Wr32(inode, Layout.InoFlags, iflags & ~0x00080000U);

        uint needed = (uint)((len + BlockSize - 1) / BlockSize);
        // ensure at least zero blocks
        uint blocks512 = 0;
        byte* tmp = stackalloc byte[BlockSize];

        // write direct + single indirect
        for (uint lb = 0; lb < needed; lb++)
        {
            uint pb = 0;
            if (lb < 12)
            {
                pb = GetBlockPtr(inode, (int)lb);
                if (pb == 0)
                {
                    if (!AllocBlock(out pb)) return false;
                    SetBlockPtr(inode, (int)lb, pb);
                }
            }
            else
            {
                uint ib = GetBlockPtr(inode, 12);
                if (ib == 0)
                {
                    if (!AllocBlock(out ib)) return false;
                    SetBlockPtr(inode, 12, ib);
                    for (int k = 0; k < BlockSize; k++) tmp[k] = 0;
                    WriteBlock(ib, tmp);
                }
                uint idx = lb - 12;
                ReadBlock(ib, tmp);
                pb = Rd32(tmp, (int)idx * 4);
                if (pb == 0)
                {
                    if (!AllocBlock(out pb)) return false;
                    Wr32(tmp, (int)idx * 4, pb);
                    WriteBlock(ib, tmp);
                }
                blocks512 += 8; // indirect block accounted below once
            }
            // write data chunk
            for (int k = 0; k < BlockSize; k++) tmp[k] = 0;
            int off = (int)lb * BlockSize;
            int take = len - off;
            if (take > BlockSize) take = BlockSize;
            if (take > 0)
            {
                for (int k = 0; k < take; k++) tmp[k] = data[off + k];
            }
            WriteBlock(pb, tmp);
            blocks512 += 8;
            if (lb >= 12 && lb == 12) blocks512 += 8; // indirect block
        }

        if (needed == 0) blocks512 = 0;
        // correct blocks512 count including indirect block if needed
        if (needed > 12)
        {
            // add 8 for indirect block once
            blocks512 += 8;
        }

        SetInodeSize(inode, (uint)len);
        SetInodeBlocks(inode, blocks512);
        ushort mode = InodeGetMode(inode);
        if (mode == 0) mode = 0x81A4;
        Wr16(inode, Layout.InoMode, (ushort)mode);
        Wr16(inode, Layout.InoLinks, (ushort)(InodeGetLinks(inode) == 0 ? 1 : InodeGetLinks(inode)));
        WriteInode(ino, inode);
        return true;
    }

    public static unsafe bool ReadFileAll(uint ino, byte* dst, int capacity, out int outLen)
    {
        outLen = 0;
        if (dst == null || capacity < 0) return false;
        if (!_mounted && !Mount()) return false;
        // use Type logic by reading size
        byte* inode = stackalloc byte[256];
        if (!ReadInode(ino, inode)) return false;
        if (!IsReg(InodeGetMode(inode))) return false;
        uint sz = InodeGetSize(inode);
        if (sz > (uint)capacity) sz = (uint)capacity;
        byte* blk = stackalloc byte[BlockSize];
        ulong off = 0;
        while (off < sz)
        {
            ulong lblk = off / (ulong)BlockSize;
            int boff = (int)(off % (ulong)BlockSize);
            uint bnum = InodeGetBlock(inode, lblk);
            if (bnum == 0) break;
            ReadBlock(bnum, blk);
            int take = BlockSize - boff;
            if ((ulong)(off + (ulong)take) > sz) take = (int)(sz - off);
            for (int k = 0; k < take; k++)
            {
                byte c = blk[boff + k];
                dst[outLen++] = c;
            }
            off += (ulong)take;
        }
        return true;
    }

    public static unsafe bool DirIsEmpty(uint dirIno, out bool empty)
    {
        empty = true;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(dirIno, inode)) return false;
        if (!IsDir(InodeGetMode(inode))) return false;
        uint size = InodeGetSize(inode);
        ulong blocks = ((ulong)size + (ulong)BlockSize - 1) / (ulong)BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[BlockSize];
        for (ulong b = 0; b < blocks; b++)
        {
            uint bnum = InodeGetBlock(inode, b);
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
                if (iino != 0 && nlen > 0)
                {
                    // name
                    bool isDot = (nlen == 1 && de[Layout.DirName] == (byte)'.');
                    bool isDotDot = (nlen == 2 && de[Layout.DirName] == (byte)'.' && de[Layout.DirName + 1] == (byte)'.');
                    if (!isDot && !isDotDot) { empty = false; return true; }
                }
                off += rlen;
                if (off > BlockSize - 8) break;
            }
        }
        return true;
    }

    public static unsafe bool DirAddEntry(uint dirIno, char* name, int nameLen, uint childIno, byte childType)
    {
        if (nameLen <= 0 || nameLen > 255) return false;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(dirIno, inode)) return false;
        if (!IsDir(InodeGetMode(inode))) return false;

        byte* nb = stackalloc byte[300];
        for (int k = 0; k < nameLen && k < 299; k++) nb[k] = (byte)name[k];

        int desired = (8 + nameLen + 3) & ~3;
        uint size = InodeGetSize(inode);
        uint blocks = size == 0 ? 0 : (size + (uint)BlockSize - 1) / (uint)BlockSize;
        if (blocks == 0) blocks = 1;

        byte* blk = stackalloc byte[BlockSize];

        // try to find slack in existing blocks
        for (uint b = 0; b < blocks; b++)
        {
            uint bnum = InodeGetBlock(inode, b);
            if (bnum == 0) continue;
            ReadBlock(bnum, blk);
            int off = 0;
            int lastOff = -1;
            while (off + 8 <= BlockSize)
            {
                byte* de = blk + off;
                ushort rlen = DirEntryRecLen(de);
                if (rlen == 0) break;
                int nlen = DirEntryNameLen(de);
                int act = (8 + nlen + 3) & ~3;
                if (act <= 8) act = 8;
                int slack = (int)rlen - act;
                if (slack >= desired)
                {
                    // shrink current entry
                    Wr16(blk, off + Layout.DirRecLen, (ushort)act);
                    int newOff = off + act;
                    // write new entry
                    Wr32(blk, newOff + Layout.DirIno, childIno);
                    Wr16(blk, newOff + Layout.DirRecLen, (ushort)(rlen - act));
                    blk[newOff + Layout.DirNameLen] = (byte)nameLen;
                    blk[newOff + Layout.DirType] = childType;
                    for (int k = 0; k < nameLen; k++) blk[newOff + Layout.DirName + k] = (byte)name[k];
                    // zero rest of name slot
                    for (int k = nameLen; k < (rlen - act) - 8 && k < BlockSize; k++) blk[newOff + Layout.DirName + k] = 0;
                    WriteBlock(bnum, blk);
                    return true;
                }
                off += rlen;
                lastOff = off;
                if (off > BlockSize - 8) break;
            }
        }

        // append new block if needed (manage up to 10 direct blocks)
        if (blocks < 10)
        {
            uint newBlock;
            if (!AllocBlock(out newBlock)) return false;
            // set inode block pointer at index blocks
            SetBlockPtr(inode, (int)blocks, newBlock);
            size = (blocks + 1) * (uint)BlockSize;
            SetInodeSize(inode, size);
            // block count 512 units: (blocks+1)*8
            SetInodeBlocks(inode, (blocks + 1) * 8);

            byte* nbk = stackalloc byte[BlockSize];
            for (int k = 0; k < BlockSize; k++) nbk[k] = 0;
            Wr32(nbk, Layout.DirIno, childIno);
            Wr16(nbk, Layout.DirRecLen, (ushort)BlockSize);
            nbk[Layout.DirNameLen] = (byte)nameLen;
            nbk[Layout.DirType] = childType;
            for (int k = 0; k < nameLen; k++) nbk[Layout.DirName + k] = (byte)name[k];
            WriteBlock(newBlock, nbk);
            WriteInode(dirIno, inode);
            return true;
        }
        return false;
    }

    public static unsafe bool DirRemoveEntry(uint dirIno, char* name, int nameLen)
    {
        if (nameLen <= 0) return false;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(dirIno, inode)) return false;
        if (!IsDir(InodeGetMode(inode))) return false;
        uint size = InodeGetSize(inode);
        uint blocks = size == 0 ? 0 : (size + (uint)BlockSize - 1) / (uint)BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[BlockSize];
        for (uint b = 0; b < blocks; b++)
        {
            uint bnum = InodeGetBlock(inode, b);
            if (bnum == 0) continue;
            ReadBlock(bnum, blk);
            int off = 0;
            int prevOff = -1;
            while (off + 8 <= BlockSize)
            {
                byte* de = blk + off;
                ushort rlen = DirEntryRecLen(de);
                if (rlen == 0) break;
                int nlen = DirEntryNameLen(de);
                if (DirEntryInode(de) != 0 && nlen == nameLen)
                {
                    bool same = true;
                    for (int k = 0; k < nameLen; k++)
                        if (de[Layout.DirName + k] != (byte)name[k]) { same = false; break; }
                    if (same)
                    {
                        if (prevOff >= 0)
                        {
                            // combine rec_len
                            ushort prevRlen = Rd16(blk, prevOff + Layout.DirRecLen);
                            Wr16(blk, prevOff + Layout.DirRecLen, (ushort)(prevRlen + rlen));
                        }
                        // mark removed
                        Wr32(blk, off + Layout.DirIno, 0);
                        blk[off + Layout.DirNameLen] = 0;
                        WriteBlock(bnum, blk);
                        return true;
                    }
                }
                prevOff = off;
                off += rlen;
                if (off > BlockSize - 8) break;
            }
        }
        return false;
    }

    public static unsafe bool DirEntryExists(uint dirIno, char* name, int nameLen, out uint childIno, out byte childType)
    {
        childIno = 0;
        childType = 0;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(dirIno, inode)) return false;
        if (!IsDir(InodeGetMode(inode))) return false;
        byte* nmb = stackalloc byte[300];
        for (int k = 0; k < nameLen && k < 299; k++) nmb[k] = (byte)name[k];
        uint size = InodeGetSize(inode);
        uint blocks = size == 0 ? 0 : (size + (uint)BlockSize - 1) / (uint)BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[BlockSize];
        for (uint b = 0; b < blocks; b++)
        {
            uint bnum = InodeGetBlock(inode, b);
            if (bnum == 0) continue;
            ReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= BlockSize)
            {
                byte* de = blk + off;
                ushort rlen = DirEntryRecLen(de);
                if (rlen == 0) break;
                uint iino = DirEntryInode(de);
                byte nlen = DirEntryNameLen(de);
                if (iino != 0 && nlen == (byte)nameLen)
                {
                    bool same = true;
                    for (int k = 0; k < nameLen; k++)
                        if (de[Layout.DirName + k] != (byte)name[k]) { same = false; break; }
                    if (same)
                    {
                        childIno = iino;
                        childType = DirEntryType(de);
                        return true;
                    }
                }
                off += rlen;
                if (off > BlockSize - 8) break;
            }
        }
        return false;
    }


    public static unsafe bool CopyFileData(uint srcIno, uint dstIno)
    {
        if (!_mounted && !Mount()) return false;
        byte* sin = stackalloc byte[256];
        if (!ReadInode(srcIno, sin)) return false;
        if (!IsReg(InodeGetMode(sin))) return false;
        uint sz = InodeGetSize(sin);

        byte* din = stackalloc byte[256];
        for (int k = 0; k < 256; k++) din[k] = 0;
        ushort mode = InodeGetMode(sin);
        if (mode == 0) mode = 0x81A4;
        Wr16(din, Layout.InoMode, mode);
        Wr16(din, Layout.InoLinks, 1);
        uint blocks = (sz + (uint)BlockSize - 1) / (uint)BlockSize;
        if (blocks == 0 && sz > 0) blocks = 1;

        byte* buf = stackalloc byte[BlockSize];
        // direct blocks
        for (uint lb = 0; lb < blocks && lb < 12; lb++)
        {
            uint spb = InodeGetBlock(sin, lb);
            uint dpb;
            if (!AllocBlock(out dpb)) return false;
            SetBlockPtr(din, (int)lb, dpb);
            if (spb != 0) { ReadBlock(spb, buf); }
            else { for (int k = 0; k < BlockSize; k++) buf[k] = 0; }
            WriteBlock(dpb, buf);
        }
        // single indirect (if needed)
        if (blocks > 12)
        {
            uint sib = InodeGetBlock(sin, 12); // not fully handled; simple
            uint dib;
            if (!AllocBlock(out dib)) return false;
            SetBlockPtr(din, 12, dib);
            for (int k = 0; k < BlockSize; k++) buf[k] = 0;
            WriteBlock(dib, buf);
            byte* ind = stackalloc byte[BlockSize];
            for (uint lb = 12; lb < blocks; lb++)
            {
                uint spb = InodeGetBlock(sin, lb);
                uint dpb;
                if (!AllocBlock(out dpb)) return false;
                int idx = (int)(lb - 12);
                Wr32(ind, idx * 4, dpb);
                if (spb != 0) { ReadBlock(spb, buf); } else { for (int k = 0; k < BlockSize; k++) buf[k] = 0; }
                WriteBlock(dpb, buf);
            }
            WriteBlock(dib, ind);
        }
        SetInodeSize(din, sz);
        uint bs = blocks * 8;
        if (blocks > 12) bs += 8;
        SetInodeBlocks(din, bs);
        WriteInode(dstIno, din);
        return true;
    }


    public static bool IsSymlink(byte* inode)
    {
        return inode != null && (InodeGetMode(inode) & 0xF000) == 0xA000;
    }

    public static bool IsSymlinkMode(ushort mode) { return (mode & 0xF000) == 0xA000; }

    public static unsafe bool ReadSymlink(uint ino, char* dst, int dstCap, out int outLen)
    {
        outLen = 0;
        if (dst == null || dstCap <= 0) return false;
        if (!_mounted && !Mount()) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(ino, inode)) return false;
        if (!IsSymlink(inode)) return false;
        uint sz = InodeGetSize(inode);
        if (sz == 0 || sz > (uint)(dstCap - 1)) sz = (uint)(dstCap - 1);
        // slow symlink: target stored in a data block (i_block[0])
        uint blkPtr = Rd32(inode, Layout.InoBlock);
        if (blkPtr != 0 && sz >= 60)
        {
            byte* blk = stackalloc byte[BlockSize];
            ReadBlock(blkPtr, blk);
            for (uint k = 0; k < sz; k++)
                dst[outLen++] = (char)blk[k];
        }
        else
        {
            // fast symlink: stored in i_block bytes
            for (uint k = 0; k < sz; k++)
                dst[outLen++] = (char)inode[Layout.InoBlock + k];
        }
        dst[outLen] = '\0';
        return true;
    }

    public static unsafe bool AllocSymlink(out uint ino, char* target, int tlen)
    {
        ino = 0;
        if (tlen < 0 || tlen >= 4096) return false;
        if (!AllocInode(out ino, 0xA1FF)) return false;
        byte* inode = stackalloc byte[256];
        for (int k = 0; k < 256; k++) inode[k] = 0;
        Wr16(inode, Layout.InoMode, 0xA1FF);
        Wr16(inode, Layout.InoLinks, 1);
        Wr32(inode, Layout.InoSizeLo, (uint)tlen);
        Wr32(inode, Layout.InoSizeHi, 0);
        Wr32(inode, Layout.InoBlocksLo, 0);
        Wr16(inode, Layout.InoBlocksHi, 0);
        if (tlen < 60)
        {
            // fast symlink: target stored in i_block bytes
            for (int k = 0; k < tlen; k++) inode[Layout.InoBlock + k] = (byte)target[k];
        }
        else
        {
            // slow symlink: target stored in a data block pointed by i_block[0]
            uint dataBlk;
            if (!AllocBlock(out dataBlk)) return false;
            byte* blk = stackalloc byte[BlockSize];
            for (int k = 0; k < BlockSize; k++) blk[k] = 0;
            for (int k = 0; k < tlen; k++) blk[k] = (byte)target[k];
            WriteBlock(dataBlk, blk);
            Wr32(inode, Layout.InoBlock, dataBlk);
            Wr32(inode, Layout.InoBlocksLo, BlockSize / 512); // i_blocks in 512-byte sectors
        }
        Wr32(inode, Layout.InoFlags, 0); // not extent
        WriteInode(ino, inode);
        return true;
    }

    // Resolve path, following one symlink at the end.
    public static unsafe bool ResolveFollow(char* path, int len, out uint ino)
    {
        ino = 0;
        if (!Resolve(path, len, out ino)) return false;
        byte* inode = stackalloc byte[256];
        if (!ReadInode(ino, inode)) return false;
        if (!IsSymlink(inode)) return true;
        char* target = stackalloc char[128];
        int tlen = 0;
        if (!ReadSymlink(ino, target, 128, out tlen)) return false;
        if (tlen <= 0) return false;
        target[tlen] = '\0';
        return Resolve(target, tlen, out ino);
    }

    // ---- little-endian 16/32-bit accessors ----
    private static ushort Rd16(byte* p, int o) { return (ushort)(p[o] | (p[o + 1] << 8)); }
    private static uint Rd32(byte* p, int o) { return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)); }
    public static void Wr16(byte* p, int o, ushort v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)(v >> 8); }
    public static void Wr32(byte* p, int o, uint v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)((v >> 8) & 0xFF); p[o + 2] = (byte)((v >> 16) & 0xFF); p[o + 3] = (byte)((v >> 24) & 0xFF); }
}