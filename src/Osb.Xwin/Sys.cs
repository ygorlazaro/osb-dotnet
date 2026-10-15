// The XWIN system-call binding: a thin wrapper over INT 21h.
//
// The kernel's syscall gate reads the function number from AH and the
// arguments from RDI/RSI/RDX/RCX, and returns in RAX. `osb_sys` (xwin.S) does
// the register shuffle and the `int $0x21`; `--directpinvoke:osbsys` turns the
// DllImport below into a plain call to that symbol.

using System.Runtime.InteropServices;

namespace Osb.Xwin;

internal static unsafe class Sys
{
    [DllImport("osbsys")] private static extern long osb_sys(long fn, long a, long b, long c, long d);

    // `fn` is the DOS-style function number (the call lands in AH); a..d map to
    // RDI/RSI/RDX/RCX.
    public static long Call(long fn, long a, long b, long c, long d) => osb_sys(fn << 8, a, b, c, d);
    public static long Call(long fn, long a, long b, long c) => osb_sys(fn << 8, a, b, c, 0);

    // Pack two signed 16-bit coordinates into one argument (low in the low
    // half, high in the high half); the kernel sign-extends both back.
    public static long Pack(int lo, int hi) => ((long)(lo & 0xFFFF) << 16) | (long)(hi & 0xFFFF);

    public static void Write(byte* p, int n) => Call(0x01, (long)p, n, 0);
    public static void Exit(int code) => osb_sys(0x4C00 | (uint)(code & 0xFF), 0, 0, 0, 0);
}
