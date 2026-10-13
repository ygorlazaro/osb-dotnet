// 8254 PIT, channel 0: a ~100 Hz square wave that drives the preemption tick.
// The tick counter lives here because everything that reports uptime or waits
// on time reads it from one place.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Cpu;

internal static class Pit
{
    public const int Hz = Layout.PitHz;

    private static ulong _ticks;              // 100 Hz heartbeat

    public static ulong Ticks => _ticks;

    public static void Init()
    {
        uint divisor = (uint)(1193182 / Hz); // 11931 ~= 100.000 Hz
        NativeMethods.outb(0x43, 0x36);                     // ch0, lobyte/hibyte, mode 3, binary
        NativeMethods.outb(0x40, (byte)(divisor & 0xFF));
        NativeMethods.outb(0x40, (byte)(divisor >> 8));
    }

    // One heartbeat, counted from the IRQ0 handler.
    public static void Tick()
    {
        _ticks++;
    }
}