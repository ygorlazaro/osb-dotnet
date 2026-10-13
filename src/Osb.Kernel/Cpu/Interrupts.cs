// Interrupt plumbing: the 8259A pair, the IDT and the single dispatcher that
// the assembly stubs (isr_common) call with a pointer to the saved frame.
//
// Frame layout (ulong offsets):
//   0..14 = rax..r15, 15 = vector, 16 = dummy error code,
//   17..21 = rip, cs, rflags, rsp, ss.

using System.Runtime;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Processes;

namespace Osb.Kernel.Cpu;

internal static unsafe class Interrupts
{
    private unsafe struct IdtTable { public fixed byte bytes[256 * 16]; }
    private unsafe struct IdtrTable { public fixed byte data[10]; } // lidt operand

    private static IdtTable _idt;
    private static IdtrTable _idtr;

    // 8259A: remap IRQ0-7 -> 0x30-0x37 and IRQ8-15 -> 0x38-0x3F (8086 mode,
    // slave on IRQ2). Vectors 0x20-0x2F are left free; 0x21 is the INT 21h
    // system-call gate. Masks: master unmask IRQ0 (PIT), IRQ1 (kbd), IRQ2
    // (cascade, required for any slave IRQ) and IRQ4 (COM1 serial console);
    // slave unmask IRQ8 (RTC).
    public static void PicInit()
    {
        NativeMethods.outb(0x20, 0x11); NativeMethods.outb(0x80, 0);  // ICW1: init, edge triggered
        NativeMethods.outb(0xA0, 0x11); NativeMethods.outb(0x80, 0);
        NativeMethods.outb(0x21, Layout.PicBase);  NativeMethods.outb(0x80, 0);  // ICW2: master base vector 0x30
        NativeMethods.outb(0xA1, Layout.PicBase + 8); NativeMethods.outb(0x80, 0); // ICW2: slave base vector 0x38
        NativeMethods.outb(0x21, 0x04); NativeMethods.outb(0x80, 0);  // ICW3: slave on master IRQ2
        NativeMethods.outb(0xA1, 0x02); NativeMethods.outb(0x80, 0);
        NativeMethods.outb(0x21, 0x01); NativeMethods.outb(0x80, 0);  // ICW4: 8086/8088 mode
        NativeMethods.outb(0xA1, 0x01); NativeMethods.outb(0x80, 0);
        NativeMethods.outb(0x21, 0xE8); NativeMethods.outb(0x80, 0);  // OCW1: unmask IRQ0 (PIT), IRQ1 (kbd), IRQ2 cascade, IRQ4 (COM1)
        NativeMethods.outb(0xA1, 0xFE); NativeMethods.outb(0x80, 0);  // OCW1: unmask IRQ8 (RTC) only
    }

    public static void Eoi(int irq)
    {
        if (irq >= 8) NativeMethods.outb(0xA0, 0x20);
        NativeMethods.outb(0x20, 0x20);
    }

    public static void Init()
    {
        // One stub per vector, address table exported by interrupts.S.
        // Vector 0x21 is the system-call gate: DPL3 so ring 3 may invoke it.
        ulong table = NativeMethods.osb_get_stub_table();
        for (int i = 0; i < 256; i++)
            WriteEntry(i, *(ulong*)(table + (ulong)i * 8),
                i == Layout.Int21Vector ? Layout.IdtUserGate : Layout.IdtInterruptGate);

        // IDTR = { limit: 16 bits, base: 64 bits } -> 10 bytes, contiguous.
        int limit = Layout.IdtSize - 1; // 4095
        ulong idtBase;
        fixed (byte* p = _idt.bytes) { idtBase = (ulong)p; }
        fixed (byte* p = _idtr.data)
        {
            p[0] = (byte)limit;
            p[1] = (byte)(limit >> 8);
            p[2] = (byte)idtBase;
            p[3] = (byte)(idtBase >> 8);
            p[4] = (byte)(idtBase >> 16);
            p[5] = (byte)(idtBase >> 24);
            p[6] = (byte)(idtBase >> 32);
            p[7] = (byte)(idtBase >> 40);
            p[8] = (byte)(idtBase >> 48);
            p[9] = (byte)(idtBase >> 56);
            NativeMethods.lidt(p);
        }
    }

    // IDT entry: block of 16 bytes written manually (byte 0: offset bits 0-7).
    private static void WriteEntry(int i, ulong handler, byte typeAttr)
    {
        int o = i * 16;
        _idt.bytes[o + 0] = (byte)handler;
        _idt.bytes[o + 1] = (byte)(handler >> 8);
        _idt.bytes[o + 2] = (byte)Layout.KernelSelector;  // selector: kernel code
        _idt.bytes[o + 3] = 0x00;
        _idt.bytes[o + 4] = 0x00;                  // IST = 0
        _idt.bytes[o + 5] = typeAttr;
        _idt.bytes[o + 6] = (byte)(handler >> 16);
        _idt.bytes[o + 7] = (byte)(handler >> 24);
        _idt.bytes[o + 8] = (byte)(handler >> 32);
        _idt.bytes[o + 9] = (byte)(handler >> 40);
        _idt.bytes[o + 10] = (byte)(handler >> 48);
        _idt.bytes[o + 11] = (byte)(handler >> 56);
        _idt.bytes[o + 12] = 0x00;
        _idt.bytes[o + 13] = 0x00;
        _idt.bytes[o + 14] = 0x00;
        _idt.bytes[o + 15] = 0x00;
    }

    // Dispatcher called from isr_common.
    [RuntimeExport("osb_handle_interrupt")]
    private static void HandleInterrupt(ulong* frame)
    {
        ulong vector = frame[15];

        if (vector == Layout.Int21Vector) // DOS-style system call from ring 3
        {
            Syscalls.Handle(frame);
            return;
        }

        if (vector == Layout.KbdVector) // IRQ1: PS/2 controller has a scancode
        {
            byte sc = NativeMethods.inb(0x60);
            Keyboard.HandleScancode(sc);
            Eoi(1);
            return;
        }

        if (vector == Layout.SerVector) // IRQ4: COM1 received byte(s) (serial console)
        {
            // Drain the UART receive FIFO: LSR bit0 = data ready.
            while ((NativeMethods.inb(0x3F8 + 5) & 0x01) != 0)
            {
                byte c = NativeMethods.inb(0x3F8);
                if (c == 0x03) ConsoleControl.RequestCtrlC();   // Ctrl+C (consume it)
                else Serial.Push(c);
            }
            Eoi(4);
            return;
        }

        if (vector == Layout.TimerVector) // IRQ0: PIT heartbeat (100 Hz)
        {
            Pit.Tick();
            Eoi(0);                    // EOI before switching: the PIC may deliver
            Scheduler.Preempt();       // the next tick while another task runs
            return;
        }

        if (vector == Layout.RtcVector) // IRQ8: RTC update finished (~1 Hz)
        {
            Rtc.OnUpdateIrq();
            Eoi(8);
            return;
        }

        if (vector < 32)
        {
            // A fault raised by ring-3 code is recovered: kill the user task and
            // keep the system running. Anything from CPL0 is fatal.
            if ((frame[18] & 3) == 3) { UserFault(vector, frame); return; }
            Panic(vector, frame);
            return;
        }

        Eoi((int)(vector - Layout.PicBase)); // masked IRQ that still slipped through
    }

    // A ring-3 program faulted (bad instruction, page fault, ...). Report it and
    // terminate just that task; the kernel and other tasks keep running.
    private static void UserFault(ulong vector, ulong* frame)
    {
        Vga.PutChar('\n');
        Vga.Write("[kernel] user program fault at CPL3: vector=");
        Vga.WriteHex(vector);
        Vga.Write(" err=");
        Vga.WriteHex(frame[16]);
        Vga.Write(" RIP=");
        Vga.WriteHex(frame[17]);
        Vga.PutChar('\n');
        Vga.WriteLine("[kernel] terminating the user task; system continues.");
        Scheduler.ExitCurrent(-1);         // fault code
    }

    private static void Panic(ulong vector, ulong* frame)
    {
        Vga.PutChar('\n');
        Vga.Write("!! CPU EXCEPTION vector=0x");
        Vga.WriteHex(vector);
        Vga.Write(" ERR=0x");
        Vga.WriteHex(frame[16]);
        Vga.Write(" RIP=0x");
        Vga.WriteHex(frame[17]);
        Vga.WriteLine("  -- system halted.");
        NativeMethods.cli();
        for (;;) NativeMethods.hlt_cpu();
    }
}