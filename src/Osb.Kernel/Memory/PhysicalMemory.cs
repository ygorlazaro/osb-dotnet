// Physical frame allocator: a flat bitmap over the RAM reported by the
// Multiboot2 memory map (capped at 1 GiB). Address spaces (AddressSpace.cs)
// and the OSLANG arena carve their blocks from here.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Memory;

internal static unsafe class PhysicalMemory
{
    // Frame bitmap: covers up to 1 GiB at 4 KiB granularity.
    private unsafe struct FrameMap { public fixed byte bits[Layout.MaxFrames / 8]; }

    private static FrameMap _frames;
    private static ulong _floor;        // first allocatable physical address
    private static ulong _top;          // end of allocatable RAM
    private static ulong _total;        // allocatable frame count
    private static ulong _used;         // allocated frame count

    public static ulong Floor => _floor;
    public static ulong Top => _top;
    public static ulong Total => _total;
    public static ulong Used => _used;

    // Walk the Multiboot2 memory map to find the biggest usable RAM region
    // above the kernel (falls back to 64 MiB, the QEMU default).
    public static void Detect(ulong info)
    {
        ulong floor = Layout.PhysFloorDefault;
        ulong top = Layout.PhysTopDefault;
        ulong bestLen = 0;

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
                        if (mtype == 1 && baddr >= 0x100000 && len > bestLen)
                        {
                            bestLen = len;
                            floor = baddr < Layout.PhysFloorDefault ? Layout.PhysFloorDefault : baddr;
                            top = baddr + len;
                        }
                        e += entrySize;
                    }
                }
                p += size;
            }
        }
        if (top > 0x40000000UL) top = 0x40000000UL;    // identity map is 1 GiB
        _floor = (floor + 0xFFF) & ~0xFFFUL;
        _top = top & ~0xFFFUL;
        if (_top <= _floor) { _floor = Layout.PhysFloorDefault; _top = Layout.PhysTopDefault; }
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
    }

    private static void Reserve(ulong lo, ulong hi)
    {
        if (lo < _floor) lo = _floor;
        if (hi > _top) hi = _top;
        ulong i = (lo - _floor) / Layout.FrameSize;
        ulong j = (hi - _floor) / Layout.FrameSize;
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