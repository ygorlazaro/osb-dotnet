// CMOS real-time clock: a software clock refreshed by the "update-ended"
// interrupt (IRQ8, ~1 Hz). The shell formats time and date from here.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Cpu;

internal static class Rtc
{
    private static byte _sec, _min, _hour, _day, _month, _year;

    public static byte Second => _sec;
    public static byte Minute => _min;
    public static byte Hour => _hour;
    public static byte Day => _day;
    public static byte Month => _month;
    public static byte Year => _year;      // two digits (00-99)

    private static byte CmosRead(byte reg)
    {
        NativeMethods.outb(Layout.RtcIndex, reg);
        return NativeMethods.inb(Layout.RtcData);
    }

    private static void CmosWrite(byte reg, byte value)
    {
        NativeMethods.outb(Layout.RtcIndex, (byte)(reg | 0x80)); // select register, NMI disabled
        NativeMethods.outb(Layout.RtcData, value);
        NativeMethods.outb(Layout.RtcIndex, reg);                // NMI back on
    }

    // Read the RTC time/date into the software clock, waiting out any update.
    private static void ReadTime()
    {
        while ((CmosRead(0x0A) & 0x80) != 0) { }   // UIP: update in progress?
        _sec = CmosRead(0x00);
        _min = CmosRead(0x02);
        _hour = CmosRead(0x04);
        _day = CmosRead(0x07);
        _month = CmosRead(0x08);
        _year = CmosRead(0x09);
    }

    public static void Init()
    {
        byte regB = CmosRead(0x0B);
        regB |= 0x02;            // 24-hour format
        regB |= 0x04;            // binary data mode
        regB |= 0x10;            // UIE: interrupt at the end of every update (~1 Hz)
        CmosWrite(0x0B, regB);
        CmosRead(0x0C);          // status C: clears any pending RTC IRQ
        ReadTime();              // seed the software clock
    }

    // IRQ8: acknowledge the interrupt and refresh the software clock.
    public static void OnUpdateIrq()
    {
        CmosRead(0x0C);          // status C: acknowledge/clear the RTC IRQ
        ReadTime();
    }

    // HH:MM:SS
    public static void PrintTime()
    {
        Vga.WriteTwo(_hour); Vga.PutChar(':');
        Vga.WriteTwo(_min); Vga.PutChar(':');
        Vga.WriteTwo(_sec);
    }

    // DD/MM/YYYY
    public static void PrintDate()
    {
        Vga.WriteTwo(_day); Vga.PutChar('/');
        Vga.WriteTwo(_month); Vga.PutChar('/');
        Vga.WriteDec(2000UL + (ulong)_year);
    }
}