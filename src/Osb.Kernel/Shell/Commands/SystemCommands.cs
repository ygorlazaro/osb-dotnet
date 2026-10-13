// Commands that do not touch files or processes: identity, console, clock,
// memory report and machine control.

using Osb.Kernel.Cpu;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class SystemCommands
{
    public static void Version()
    {
        Vga.WriteLine("OSB (dotnet) version 0.1 - bootable kernel");
    }

    public static void About()
    {
        Vga.WriteLine("OSB: Operating System Basic, created in 2000, rebooted in C#.");
    }

    public static void Help()
    {
        Vga.WriteLine("Commands: VER  ABOUT  HELP  CLS  ECHO <text>  SET  TICKS");
        Vga.WriteLine("          SET [NAME=value]  env ($VAR, case-insensitive names)");
        Vga.WriteLine("Files:    DIR [path] [/W]  CD [path]  TYPE <file>  (ext4/A:)");
        Vga.WriteLine("          OSLANG <script.OSL> [args]  run an OSLANG script (A:)");
        Vga.WriteLine("          SLEEP   TIME  DATE  MEM  HALT  POWEROFF  REBOOT");
        Vga.WriteLine("Processes:SPAWN [BAD|SPAWNER] [&]  start a ring-3 process");
        Vga.WriteLine("          RING3 [BAD] [&]  synonym for SPAWN");
        Vga.WriteLine("          TASK / PS        list processes (a simple ps)");
        Vga.WriteLine("          KILL <pid>       terminate a process");
        Vga.WriteLine("          WAIT <pid>       wait for a child and collect its code");
        Vga.WriteLine("          TAB <pid>        bring a process to the foreground");
        Vga.WriteLine("          DEMO [&]         start a demo (kernel) task");
        Vga.WriteLine("          Add '&' to run in the background; Ctrl+C aborts a");
        Vga.WriteLine("          foreground job and returns to the shell.");
    }

    public static void Clear()
    {
        Vga.Clear();
    }

    // MEM: what the frame allocator is holding and what range it covers.
    public static void Memory()
    {
        Vga.Write("Physical frames: ");
        Vga.WriteDec(PhysicalMemory.Used);
        Vga.Write(" used / ");
        Vga.WriteDec(PhysicalMemory.Total);
        Vga.Write(" total  (");
        Vga.WriteDec((PhysicalMemory.Top - PhysicalMemory.Floor) / 1024);
        Vga.WriteLine(" KiB usable)");
        Vga.Write("Range: ");
        Vga.WriteHex(PhysicalMemory.Floor);
        Vga.Write(" - ");
        Vga.WriteHex(PhysicalMemory.Top);
        Vga.WriteLine("");
    }

    public static void Time()
    {
        Rtc.PrintTime();
        Vga.PutChar('\n');
    }

    public static void Date()
    {
        Rtc.PrintDate();
        Vga.PutChar('\n');
    }

    // TICKS: uptime, in seconds and in timer ticks.
    public static void Ticks()
    {
        Vga.Write("Uptime: ");
        Vga.WriteDec(Pit.Ticks / (ulong)Pit.Hz);
        Vga.Write(" s (");
        Vga.WriteDec(Pit.Ticks);
        Vga.WriteLine(" ticks)");
    }

    public static void Sleep(char* line, int len)
    {
        int i = 5;
        while (i < len && line[i] == ' ') i++;
        ulong secs = Vga.ParseUInt(line, i, len);
        Vga.WriteLine("Sleeping ...");
        ulong target = Pit.Ticks + secs * (ulong)Pit.Hz;
        while (Pit.Ticks < target) NativeMethods.halt(); // tick-driven wait; keys queue up
        Vga.WriteLine("Done.");
    }

    public static void Echo(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        for (; i < len; i++)
            Vga.PutChar(line[i]);
        Vga.PutChar('\n');
    }

    public static void Halt()
    {
        Vga.WriteLine("System halted. (CPU stopped; in QEMU press Ctrl+Alt+2 then");
        Vga.WriteLine("'quit', or use POWEROFF to close the emulator cleanly.)");
        NativeMethods.cli();
        for (;;) NativeMethods.hlt_cpu();
    }

    public static void PowerOff()
    {
        Vga.WriteLine("Powering off (ACPI S5) ...");
        NativeMethods.cli();
        NativeMethods.osb_poweroff();                 // QEMU exits; on real HW this is S5
        for (;;) NativeMethods.hlt_cpu();             // fallback if ACPI is unavailable
    }

    public static void Reboot()
    {
        Vga.WriteLine("Rebooting ...");
        NativeMethods.osb_reboot();                   // 8042 reset; never returns
        for (;;) NativeMethods.hlt_cpu();
    }
}