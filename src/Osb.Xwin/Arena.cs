// A tiny bump allocator over a fixed block in .bss.
//
// The freestanding runtime has no heap, and XWIN allocates only a handful of
// long-lived structures (window list, icon list, string literals materialised
// at startup), so a monotonic arena is enough; nothing is ever freed.

namespace Osb.Xwin;

internal static unsafe class Arena
{
    private struct Buf { public fixed byte data[256 * 1024]; }

    private static Buf _buf;
    private static byte* _base;
    private static int _used;

    public static void Init()
    {
        fixed (byte* b = _buf.data) _base = b;
        _used = 0;
    }

    public static void* Alloc(int n)
    {
        n = (n + 15) & ~15;
        byte* p = _base + _used;
        _used += n;
        return p;
    }
}
