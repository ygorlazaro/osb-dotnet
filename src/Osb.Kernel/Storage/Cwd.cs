// DOS-like namespace state: the current drive plus the current directory of
// drive A:. The path is kept as the canonical absolute form ("A:\OSB\BIN") in
// bytes, exactly as Ext4.ResolveCanonical hands it back. The shell reads it for
// the $P prompt code and the file commands read it for relative paths.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Storage;

internal static unsafe class Cwd
{
    private const int MaxPath = 160;          // CwdState.path capacity

    private unsafe struct State { public fixed byte path[MaxPath]; }

    private static State _state;
    private static int _len;

    // Current drive letter (only A: is mounted today).
    public static char Drive => 'A';

    // The boot value is the root; anything else means "not initialised yet".
    private static void EnsureInit()
    {
        if (_len == 0)
        {
            _state.path[0] = (byte)'A';
            _state.path[1] = (byte)':';
            _state.path[2] = (byte)'\\';
            _len = 3;
        }
    }

    // Copies the canonical path out as raw bytes (it is not NUL-terminated).
    public static int CopyTo(byte* dst, int max)
    {
        EnsureInit();
        int n = _len;
        if (n > max) n = max;
        for (int i = 0; i < n; i++) dst[i] = _state.path[i];
        return n;
    }

    // Adopts a canonical path produced by Ext4.ResolveCanonical.
    public static void Set(byte* canonical, int len)
    {
        if (len <= 0 || len > MaxPath) return;
        for (int i = 0; i < len; i++) _state.path[i] = canonical[i];
        _len = len;
    }

    // Writes the current directory to the console (prompt $P).
    public static void Print()
    {
        EnsureInit();
        for (int i = 0; i < _len; i++) Vga.PutChar((char)_state.path[i]);
    }
}