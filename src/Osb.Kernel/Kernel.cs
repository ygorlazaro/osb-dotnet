// OSB kernel entry point.
//
// Freestanding C# (NativeAOT, no runtime): this file is only the boot
// sequence - it brings up the machine in order and hands control to the shell.
// Every subsystem lives in its own component:
//
//   Hardware/    Vga, Serial, Keyboard, ConsoleControl, NativeMethods (port I/O)
//   Memory/      Layout (constants), PhysicalMemory (frame allocator)
//   Cpu/         Pit, Rtc, Gdt (ring 3 + TSS), Interrupts (PIC/IDT), Syscalls
//   Processes/   Scheduler (task table + round-robin), AddressSpace (per-process CR3)
//   Storage/     Ata (PIO), Ext4 (reader/writer), Cwd (current drive/directory)
//   Shell/       Shell (loop + dispatch), Environment, Prompt, JobControl,
//                ShellText, Commands/*

using System.Runtime;
using Osb.Kernel.Cpu;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Processes;
using Osb.Kernel.Shell;

internal static unsafe partial class Kernel
{
    // Entry point (named "Main" so the assembly has a managed entry point; the
    // real entry is kmain, called by _start in boot.S).
    private static void Main() { }

    [RuntimeExport("kmain")]
    private static void KmMain(ulong multibootInfo)
    {
        Serial.Init();
        Vga.Clear();

        Interrupts.PicInit();   // remap 8259A (IRQ0-15 -> vectors 0x30-0x3F)
        Pit.Init();             // 8254 channel 0: square wave @ 100 Hz
        Rtc.Init();             // CMOS/RTC: 24h + binary + update-ended IRQ (~1 Hz)
        Gdt.Init(Scheduler.KernelStackTop(1));   // user segments + TSS, then load GDT + ltr
        PhysicalMemory.Detect(multibootInfo);   // find how much RAM we can hand out
        PhysicalMemory.Init();   // physical frame allocator (bitmap)
        Interrupts.Init();      // fill + load the IDT (exception + IRQ stubs)
        Scheduler.Init();       // console (slot 0) + kernel address space

        Vga.WriteLine("OSB - Operating System Basic");
        Vga.WriteLine("Bootable kernel prototype (C# / NativeAOT, bare metal)");
        Vga.WriteLine("Keyboard: PS/2 IRQ1 + serial console IRQ4 (COM1).");
        Vga.WriteLine("Timer: PIT channel 0 @ 100 Hz (IRQ0 ticks).");
        Vga.WriteLine("Clock: RTC via IRQ8 (update-ended).");
        Vga.WriteLine("Scheduler: preemptive round-robin over a task table.");
        Vga.WriteLine("Processes: private address space per process (CR3).");
        Vga.WriteLine("Ring 3: user segments + TSS; syscalls via INT 21h (DOS-style).");
        Vga.WriteLine("Jobs: DEMO / SPAWN / RING3; TASK / KILL / TAB / WAIT.");
        Vga.WriteLine("Files: ATA PIO + ext4 on A: (DIR / CD / TYPE)");
        Vga.WriteLine("OSLANG: interpreter on A: (OSLANG script.osl [args])");
        Vga.WriteLine("Type HELP for commands.");
        Vga.WriteLine("");

        Environment.Init();     // session environment (PATH, PROMPT, USER, ...)
        Shell.Run();            // never returns
    }
}