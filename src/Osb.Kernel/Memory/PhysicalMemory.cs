// Physical frame allocator: a flat bitmap over the RAM reported by the
// Multiboot2 memory map (capped at the 64 GiB covered by the boot-time
// identity map). Address spaces (AddressSpace.cs) and the OSLANG arena
// carve their blocks from here.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Memory;

internal static unsafe class PhysicalMemory
{
    // Frame bitmap: covers the mapped 64 GiB at 4 KiB granularity.
    private unsafe struct FrameMap { public fixed byte bits[Layout.MaxFrames / 8]; }

    private static FrameMap _frames;
    private static ulong _floor;        // first allocatable physical address
    private static ulong _top;          // end of allocatable RAM
    private static ulong _total;        // allocatable frame count
    private static ulong _used;         // allocated frame count

    // Usable regions and the gaps between them, so the allocator can span
    // RAM on both sides of the 4 GiB hole without ever handing out the
    // firmware/MMIO holes in between.
    private const int MaxRegions = 32;
    private const int MaxHoles = 33;
    private unsafe struct RegionTable { public fixed ulong bases[MaxRegions]; public fixed ulong lens[MaxRegions]; }
    private static RegionTable _regions;
    private static int _regionCount;
    private unsafe struct HoleTable { public fixed ulong lo[MaxHoles]; public fixed ulong hi[MaxHoles]; }
    private static HoleTable _holes;
    private static int _holeCount;

    public static ulong Floor => _floor;
    public static ulong Top => _top;
    public static ulong Total => _total;
    public static ulong Used => _used;

    // Walk the Multiboot2 memory map and span every usable (type 1) region
    // above the kernel, recording the gaps between them as holes. Handles
    // machines with RAM above the 4 GiB hole, where picking only the single
    // largest region would throw away the low memory (or the high memory).
    public static void Detect(ulong info)
    {
        // The kernel image grew (the frame bitmap lives in .bss), so the
        // floor must follow the real end of the image rather than a fixed
        // constant: handing out frames below it would corrupt the kernel.
        ulong kEnd = NativeMethods.osb_kernel_end();
        ulong floor = (kEnd > Layout.PhysFloorDefault ? kEnd : Layout.PhysFloorDefault);
        floor = (floor + 0xFFF) & ~0xFFFUL;

        _regionCount = 0;
        fixed (ulong* bases = _regions.bases)
        fixed (ulong* lens = _regions.lens)
        {
            if (info != 0)
            {
                uint total = *(uint*)info;
                ulong p = info + 8;
                ulong end = info + total;
                while (p + 8 <= end)
                {
                    uint type = *(uint*)p;
                    uint size = *(uint*)(p + 4);
                    if (type == 0) break;              // end tag
                    if (size < 8) break;
                    if (type == 6 && size >= 16)       // memory map tag
                    {
                        uint entrySize = *(uint*)(p + 8);
                        ulong e = p + 16;
                        ulong eend = p + size;
                        while (entrySize != 0 && e + entrySize <= eend)
                        {
                            ulong baddr = *(ulong*)e;
                            ulong len = *(ulong*)(e + 8);
                            uint mtype = *(uint*)(e + 16);
                            if (mtype == 1 && len > 0 && _regionCount < MaxRegions)
                            {
                                bases[_regionCount] = baddr;
                                lens[_regionCount] = len;
                                _regionCount++;
                            }
                            e += entrySize;
                        }
                    }
                    // Tags are 8-byte aligned on the wire. A tag whose size is not
                    // a multiple of 8 (GRUB emits e.g. size=12 for the module-align
                    // tag) would otherwise step p into the middle of the next
                    // header and desync the whole walk.
                    p += (size + 7) & ~7UL;
                }
            }

            // Keep only the part of each region that lies inside the identity
            // map and above the kernel, then sort by base (the map is usually
            // ordered already, but do not rely on it).
            int n = 0;
            for (int i = 0; i < _regionCount; i++)
            {
                ulong b = bases[i];
                ulong l = lens[i];
                if (b < floor) { l -= floor - b; b = floor; }      // clip below
                if (b + l > Layout.PhysMapLimit) l = Layout.PhysMapLimit - b;
                if (l == 0) continue;
                // insertion sort into bases[0..n)
                int j = n;
                while (j > 0 && bases[j - 1] > b) { bases[j] = bases[j - 1]; lens[j] = lens[j - 1]; j--; }
                bases[j] = b; lens[j] = l; n++;
            }
            _regionCount = n;

            if (_regionCount == 0)
            {
                // No usable map entry: fall back to the window above the image.
                _floor = floor;
                _top = Layout.PhysTopDefault;
                _holeCount = 0;
                return;
            }

            _floor = bases[0];
            _top = bases[_regionCount - 1] + lens[_regionCount - 1];

            // Every gap inside the span is a hole: firmware tables, MMIO and
            // other reserved areas must never be handed to a caller.
            _holeCount = 0;
            fixed (ulong* hlo = _holes.lo)
            fixed (ulong* hhi = _holes.hi)
            {
                ulong cursor = _floor;
                for (int i = 0; i < _regionCount; i++)
                {
                    if (bases[i] > cursor && _holeCount < MaxHoles)
                    {
                        hlo[_holeCount] = cursor;
                        hhi[_holeCount] = bases[i];
                        _holeCount++;
                    }
                    ulong e2 = bases[i] + lens[i];
                    if (e2 > cursor) cursor = e2;
                }
                if (cursor < _top && _holeCount < MaxHoles)
                {
                    hlo[_holeCount] = cursor;
                    hhi[_holeCount] = _top;
                    _holeCount++;
                }
            }
        }
    }

    public static void Init()
    {
        _total = (_top - _floor) / Layout.FrameSize;
        if (_total > (ulong)Layout.MaxFrames) _total = (ulong)Layout.MaxFrames;
        for (int i = 0; i < Layout.MaxFrames / 8; i++) _frames.bits[i] = 0;
        _used = 0;

        // Never hand out the pd[2] window: inside VA 0x400000..0x5FFFFF the
        // virtual address is remapped, so the kernel could not reach a fresh
        // block through its physical address there.
        Reserve(Layout.ReservedLo, Layout.ReservedHi);

        // Never hand out the gaps the memory map reported between usable
        // regions (firmware tables, MMIO, reserved areas).
        fixed (ulong* hlo = _holes.lo)
        fixed (ulong* hhi = _holes.hi)
        {
            for (int i = 0; i < _holeCount; i++) Reserve(hlo[i], hhi[i]);
        }
    }

    private static void Reserve(ulong lo, ulong hi)
    {
        if (lo < _floor) lo = _floor;
        if (hi > _top) hi = _top;
        // The kernel image may have grown past the reserved window, or the
        // window may sit above the detected RAM; either way the subtraction
        // below would underflow and the loop would run to the end of memory.
        if (hi <= lo) return;
        ulong i = (lo - _floor) / Layout.FrameSize;
        ulong j = (hi - _floor) / Layout.FrameSize;
        if (j > _total) j = _total;
        for (; i < j; i++) Set(i);
    }

    private static bool IsSet(ulong i) { return (_frames.bits[i >> 3] & (byte)(1 << (int)(i & 7))) != 0; }

    private static void Set(ulong i)
    {
        _frames.bits[i >> 3] |= (byte)(1 << (int)(i & 7));
        _used++;
    }

    private static void Clear(ulong i) { _frames.bits[i >> 3] &= (byte)~(1 << (int)(i & 7)); }

    // Find n consecutive free frames at the given alignment (bytes).
    public static ulong Alloc(int n, ulong align)
    {
        ulong stride = align / Layout.FrameSize;
        if (stride < 1) stride = 1;
        for (ulong i = 0; i + (ulong)n <= _total; i += stride)
        {
            bool ok = true;
            for (int j = 0; j < n; j++)
                if (IsSet(i + (ulong)j)) { ok = false; break; }
            if (!ok) continue;
            for (int j = 0; j < n; j++) Set(i + (ulong)j);
            return _floor + i * Layout.FrameSize;
        }
        return 0;
    }

    public static void Free(ulong phys, int n)
    {
        if (phys < _floor || phys >= _top) return;
        ulong i = (phys - _floor) / Layout.FrameSize;
        for (int j = 0; j < n; j++)
        {
            ulong k = i + (ulong)j;
            if (k >= _total) break;
            if (IsSet(k)) { Clear(k); if (_used > 0) _used--; }
        }
    }
}