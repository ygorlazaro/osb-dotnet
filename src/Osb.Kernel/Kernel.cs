// OSB kernel - bootable version with IDT + PIC.
// Freestanding C# (NativeAOT, no runtime): VGA text output + COM1 serial,
// PS/2 keyboard input via IRQ1 (interrupt-driven, no polling), and the
// first OSB command prompt.

using System;
using System.Runtime;
using System.Runtime.InteropServices;

internal static unsafe class Kernel
{
    // ---- hardware access (implemented in boot.S / interrupts.S) ----
    [DllImport("osbnative")] private static extern void outb(ushort port, byte value);
    [DllImport("osbnative")] private static extern byte inb(ushort port);
    [DllImport("osbnative")] private static extern void lidt(byte* idtr);
    [DllImport("osbnative")] private static extern ulong osb_get_stub_table();
    [DllImport("osbnative")] private static extern void sti();
    [DllImport("osbnative")] private static extern void cli();
    [DllImport("osbnative")] private static extern void halt();   // sti; hlt
    [DllImport("osbnative")] private static extern void hlt_cpu(); // bare hlt
    [DllImport("osbnative")] private static extern void osb_switch(ulong* saveSp, ulong* loadSp);
    [DllImport("osbnative")] private static extern ulong osb_sched_get_demo_entry();
    [DllImport("osbnative")] private static extern ulong osb_sched_get_user_entry();
    [DllImport("osbnative")] private static extern void osb_poweroff(); // ACPI S5 (QEMU exits)
    [DllImport("osbnative")] private static extern void osb_reboot();   // 8042 reset pulse
    // ring-3 plumbing (boot.S)
    [DllImport("osbnative")] private static extern void osb_lgdt(byte* gdtr);
    [DllImport("osbnative")] private static extern void osb_ltr(ushort selector);
    [DllImport("osbnative")] private static extern void osb_reload_segments();
    [DllImport("osbnative")] private static extern void osb_enter_user(ulong entry, ulong stackTop);
    // user-blob bounds (user.S)
    [DllImport("osbnative")] private static extern ulong osb_userstub_begin();
    [DllImport("osbnative")] private static extern ulong osb_userstub_end();
    [DllImport("osbnative")] private static extern ulong osb_userbad_begin();
    [DllImport("osbnative")] private static extern ulong osb_userbad_end();

    private const uint VgaBase = 0xB8000;
    private const int VgaWidth = 80;
    private const int VgaHeight = 25;
    private const byte Attr = 0x07; // light grey on black

    private static int _row;
    private static int _col;

    // ---- IDT + PIC state (fixed buffers in .bss, zero-initialized) ----
    private const int IdtSize = 256 * 16;          // 256 entries x 16 bytes
    private const ushort KernelSelector = 0x08;    // GDT: 64-bit code at offset 8
    private const byte IdtInterruptGate = 0x8E;    // present, DPL0, 64-bit gate
    private const byte IdtUserGate = 0xEE;         // present, DPL3 (callable via INT)
    // The PIC is remapped to vectors 0x30..0x3F so that vector 0x21 is free for
    // the DOS-style system-call entry point (INT 21h).
    private const int PicBase = 0x30;
    private const int KbdVector = PicBase + 1;     // IRQ1 -> vector 0x31
    private const int TimerVector = PicBase + 0;   // IRQ0 -> vector 0x30
    private const int RtcVector = PicBase + 8;     // IRQ8 -> vector 0x38
    private const int Int21Vector = 0x21;          // DOS-style syscall gate
    private const int PitHz = 100;                 // PIT frequency in Hz

    // ---- ring 3 / user memory layout ----
    private const ulong UserBase = 0x400000;       // user code/data region start
    private const ulong UserLimit = 0x600000;      // end of the U/S=1 2 MiB page
    private const ulong UserProgramAddr = 0x400000;// normal stub (copied here)
    private const ulong UserBadAddr = 0x401000;    // "supervisor escape" stub
    private const ulong UserStackTop = 0x480000;   // user stack grows down from here

    // GDT selectors (64-bit flat, base 0)
    private const ushort KernelDataSel = 0x10;
    private const ushort UserCodeSel = 0x18;       // RPL3 -> 0x1B in CS
    private const ushort UserDataSel = 0x20;       // RPL3 -> 0x23 in SS/DS
    private const ushort TssSel = 0x28;

    private const byte RtcIndex = 0x70;            // CMOS register select
    private const byte RtcData = 0x71;             // CMOS data port

    private unsafe struct IdtTable { public fixed byte bytes[256 * 16]; }
    private unsafe struct IdtrTable { public fixed byte data[10]; } // lidt operand
    private unsafe struct KbdRing
    {
        public fixed byte data[128];
        public int head;
        public int tail;
    }
    private unsafe struct GdtTable { public fixed byte bytes[7 * 8]; }  // 7 descriptors
    private unsafe struct GdtrTable { public fixed byte data[10]; }
    private unsafe struct TssStruct { public fixed byte bytes[104]; }

    private static IdtTable _idt;
    private static IdtrTable _idtr;
    private static GdtTable _gdt;
    private static GdtrTable _gdtr;
    private static TssStruct _tss;
    private static KbdRing _kbd;
    private static ulong _ticks;                    // 100 Hz heartbeat

    // Software clock maintained by the RTC "update-ended" interrupt (IRQ8).
    private static byte _rtcSec, _rtcMin, _rtcHour, _rtcDay, _rtcMonth, _rtcYear;

    // Preemptive round-robin scheduler. Task 0 = shell (kernel stack), task 1 =
    // demo, task 2 = user program (runs in ring 3 on its own kernel stack, whose
    // top is TSS.RSP0). The PIT handler switches tasks; nothing yields.
    private const int TaskShell = 0;
    private const int TaskDemo = 1;
    private const int TaskUser = 2;
    private const int TaskCount = 3;
    private const int TaskDone = 0;
    private const int TaskReady = 1;
    private unsafe struct TaskTable
    {
        public fixed ulong sp[TaskCount];
        public fixed int state[TaskCount];
    }
    private unsafe struct TaskRegion { public fixed byte bytes[16384]; }
    private static TaskTable _tasks;
    private static TaskRegion _demoStack;
    private static TaskRegion _userKStack;
    private static int _currentTask;
    private static ulong _demoDeadline;
    private static ulong _demoCount;
    private static ulong _userEntry;                // where ring 3 starts for the user task

    // Entry point (named "Main" so the assembly has a managed entry point; the
    // real entry is kmain, called by _start in boot.S).
    private static void Main() { }

    [RuntimeExport("kmain")]
    private static void KmMain(ulong multibootInfo)
    {
        SerialInit();
        VgaClear();

        PicInit();          // remap 8259A (IRQ0-15 -> vectors 0x30-0x3F)
        PitInit();          // 8254 channel 0: square wave @ 100 Hz
        RtcInit();          // CMOS/RTC: 24h + binary + update-ended IRQ (~1 Hz)
        GdtInit();          // user segments + TSS, then load the new GDT + ltr
        LoadUserBlobs();    // copy the ring-3 test programs into user memory
        IdtInit();          // fill + load the IDT (exception + IRQ stubs)
        SchedInit();        // seed the demo and user task stacks

        WriteLine("OSB - Operating System Basic");
        WriteLine("Bootable kernel prototype (C# / NativeAOT, bare metal)");
        WriteLine("Keyboard: PS/2 IRQ1 via IDT + PIC (interrupt-driven).");
        WriteLine("Timer: PIT channel 0 @ 100 Hz (IRQ0 ticks).");
        WriteLine("Clock: RTC via IRQ8 (update-ended).");
        WriteLine("Scheduler: preemptive round-robin (PIT preempts, no yields).");
        WriteLine("Ring 3: user segments + TSS; syscalls via INT 21h (DOS-style).");
        Write("Multiboot2 info @ ");
        WriteHex(multibootInfo);
        PutChar('\n');
        WriteLine("Type HELP for commands.");
        WriteLine("");

        ShellLoop();
    }

    // ---------------------------------------------------------------- console
    private static void VgaClear()
    {
        ushort* vga = (ushort*)VgaBase;
        for (int i = 0; i < VgaWidth * VgaHeight; i++)
            vga[i] = (ushort)(' ' | (Attr << 8));
        _row = 0;
        _col = 0;
    }

    private static void VgaScroll()
    {
        ushort* vga = (ushort*)VgaBase;
        for (int i = 0; i < (VgaHeight - 1) * VgaWidth; i++)
            vga[i] = vga[i + VgaWidth];
        for (int i = (VgaHeight - 1) * VgaWidth; i < VgaHeight * VgaWidth; i++)
            vga[i] = (ushort)(' ' | (Attr << 8));
        _row = VgaHeight - 1;
    }

    private static void PutChar(char c)
    {
        SerialPut(c);
        if (c == '\n')
        {
            _col = 0;
            _row++;
        }
        else if (c == '\r')
        {
            _col = 0;
        }
        else
        {
            ushort* vga = (ushort*)VgaBase;
            vga[_row * VgaWidth + _col] = (ushort)(c | (Attr << 8));
            _col++;
            if (_col >= VgaWidth)
            {
                _col = 0;
                _row++;
            }
        }
        if (_row >= VgaHeight)
            VgaScroll();
    }

    private static void Write(string s)
    {
        if (s == null) return;
        fixed (char* p = s)
        {
            for (int i = 0; i < s.Length; i++)
                PutChar(p[i]);
        }
    }

    private static void WriteLine(string s)
    {
        Write(s);
        PutChar('\n');
    }

    private static void WriteHex(ulong v)
    {
        Write("0x");
        bool started = false;
        for (int i = 15; i >= 0; i--)
        {
            int nib = (int)((v >> (i * 4)) & 0xF);
            if (nib != 0) started = true;
            if (!started && i != 0) continue;
            PutChar((char)(nib < 10 ? '0' + nib : 'A' + nib - 10));
        }
    }

    private static void WriteDec(ulong v)
    {
        char* buf = stackalloc char[20];
        int n = 0;
        if (v == 0) { PutChar('0'); return; }
        while (v != 0)
        {
            buf[n++] = (char)('0' + (int)(v % 10));
            v /= 10;
        }
        while (n > 0) PutChar(buf[--n]);
    }

    private static void WriteTwo(int v)
    {
        if (v < 10) PutChar('0');
        WriteDec((ulong)v);
    }

    private static ulong ParseUInt(char* s, int start, int end)
    {
        ulong v = 0;
        for (int i = start; i < end; i++)
        {
            if (s[i] < '0' || s[i] > '9') break;
            v = v * 10 + (ulong)(s[i] - '0');
        }
        return v;
    }

    // ---------------------------------------------------------------- serial
    private static void SerialInit()
    {
        outb(0x3F8 + 1, 0x00);
        outb(0x3F8 + 3, 0x80);
        outb(0x3F8 + 0, 0x01);
        outb(0x3F8 + 1, 0x00);
        outb(0x3F8 + 3, 0x03);
        outb(0x3F8 + 2, 0xC7);
        outb(0x3F8 + 4, 0x0B);
    }

    private static void SerialPut(char c)
    {
        while ((inb(0x3F8 + 5) & 0x20) == 0) { }
        outb(0x3F8, (byte)c);
    }

    // ------------------------------------------------------------ interrupts
    // IDT entry: block of 16 bytes written manually (byte 0: offset bits 0-7).
    private static void IdtWriteEntry(int i, ulong handler, byte typeAttr)
    {
        int o = i * 16;
        _idt.bytes[o + 0] = (byte)handler;
        _idt.bytes[o + 1] = (byte)(handler >> 8);
        _idt.bytes[o + 2] = (byte)KernelSelector;  // selector: kernel code
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

    private static void IdtInit()
    {
        // One stub per vector, address table exported by interrupts.S.
        // Vector 0x21 is the system-call gate: DPL3 so ring 3 may invoke it.
        ulong table = osb_get_stub_table();
        for (int i = 0; i < 256; i++)
            IdtWriteEntry(i, *(ulong*)(table + (ulong)i * 8),
                i == Int21Vector ? IdtUserGate : IdtInterruptGate);

        // IDTR = { limit: 16 bits, base: 64 bits } -> 10 bytes, contiguous.
        int limit = IdtSize - 1; // 4095
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
            lidt(p);
        }
    }

    // 8259A: remap IRQ0-7 -> 0x30-0x37 and IRQ8-15 -> 0x38-0x3F (8086 mode,
    // slave on IRQ2). Vectors 0x20-0x2F are left free; 0x21 is the INT 21h
    // system-call gate. Masks: master unmask IRQ0 (PIT), IRQ1 (kbd) and IRQ2
    // (cascade, required for any slave IRQ); slave unmask IRQ8 (RTC).
    private static void PicInit()
    {
        outb(0x20, 0x11); outb(0x80, 0);  // ICW1: init, edge triggered
        outb(0xA0, 0x11); outb(0x80, 0);
        outb(0x21, PicBase);  outb(0x80, 0);  // ICW2: master base vector 0x30
        outb(0xA1, PicBase + 8); outb(0x80, 0); // ICW2: slave base vector 0x38
        outb(0x21, 0x04); outb(0x80, 0);  // ICW3: slave on master IRQ2
        outb(0xA1, 0x02); outb(0x80, 0);
        outb(0x21, 0x01); outb(0x80, 0);  // ICW4: 8086/8088 mode
        outb(0xA1, 0x01); outb(0x80, 0);
        outb(0x21, 0xF8); outb(0x80, 0);  // OCW1: unmask IRQ0 (PIT), IRQ1 (kbd), IRQ2 cascade
        outb(0xA1, 0xFE); outb(0x80, 0);  // OCW1: unmask IRQ8 (RTC) only
    }

    // 8254 PIT, channel 0: 1193182 Hz / divisor -> ~100 Hz square wave on IRQ0.
    private static void PitInit()
    {
        uint divisor = (uint)(1193182 / PitHz); // 11931 ~= 100.000 Hz
        outb(0x43, 0x36);                      // ch0, lobyte/hibyte, mode 3, binary
        outb(0x40, (byte)(divisor & 0xFF));
        outb(0x40, (byte)(divisor >> 8));
    }

    // ------------------------------------------------------------ real-time clock
    private static byte CmosRead(byte reg)
    {
        outb(RtcIndex, reg);
        return inb(RtcData);
    }

    private static void CmosWrite(byte reg, byte value)
    {
        outb(RtcIndex, (byte)(reg | 0x80)); // select register, NMI disabled
        outb(RtcData, value);
        outb(RtcIndex, reg);                // NMI back on
    }

    // Read the RTC time/date into the software clock, waiting out any update.
    private static void ReadRtcTime()
    {
        while ((CmosRead(0x0A) & 0x80) != 0) { }   // UIP: update in progress?
        _rtcSec = CmosRead(0x00);
        _rtcMin = CmosRead(0x02);
        _rtcHour = CmosRead(0x04);
        _rtcDay = CmosRead(0x07);
        _rtcMonth = CmosRead(0x08);
        _rtcYear = CmosRead(0x09);
    }

    private static void RtcInit()
    {
        byte regB = CmosRead(0x0B);
        regB |= 0x02;            // 24-hour format
        regB |= 0x04;            // binary data mode
        regB |= 0x10;            // UIE: interrupt at the end of every update (~1 Hz)
        CmosWrite(0x0B, regB);
        CmosRead(0x0C);          // status C: clears any pending RTC IRQ
        ReadRtcTime();           // seed the software clock
    }

    private static void PicEoi(int irq)
    {
        if (irq >= 8) outb(0xA0, 0x20);
        outb(0x20, 0x20);
    }

    // Scancode ring buffer filled by the IRQ1 handler, drained by the shell.
    private static void KbdPush(byte sc)
    {
        int next = _kbd.tail + 1;
        if (next >= 128) next = 0;
        if (next != _kbd.head)
        {
            _kbd.data[_kbd.tail] = sc;
            _kbd.tail = next;
        }
    }

    private static int KbdPop()
    {
        if (_kbd.head == _kbd.tail) return -1;
        byte sc = _kbd.data[_kbd.head];
        int next = _kbd.head + 1;
        if (next >= 128) next = 0;
        _kbd.head = next;
        return sc;
    }

    // Dispatcher called from isr_common. Frame layout (ulong offsets):
    //   0..14 = rax..r15, 15 = vector, 16 = dummy error code,
    //   17..21 = rip, cs, rflags, rsp, ss.
    [RuntimeExport("osb_handle_interrupt")]
    private static void HandleInterrupt(ulong* frame)
    {
        ulong vector = frame[15];

        if (vector == Int21Vector) // DOS-style system call from ring 3
        {
            HandleSyscall(frame);
            return;
        }

        if (vector == KbdVector) // IRQ1: PS/2 controller has a scancode
        {
            byte sc = inb(0x60);
            KbdPush(sc);
            PicEoi(1);
            return;
        }

        if (vector == TimerVector) // IRQ0: PIT heartbeat (100 Hz)
        {
            _ticks++;
            PicEoi(0);            // EOI before switching: the PIC may deliver
            SchedulerTick();      // the next tick while another task runs
            return;
        }

        if (vector == RtcVector) // IRQ8: RTC update finished (~1 Hz)
        {
            CmosRead(0x0C);         // status C: acknowledge/clear the RTC IRQ
            ReadRtcTime();
            PicEoi(8);
            return;
        }

        if (vector < 32)
        {
            // A fault raised by ring-3 code is recovered: kill the user task and
            // keep the system running. Anything from CPL0 is fatal.
            if ((frame[18] & 3) == 3) { UserFault(vector, frame); return; }
            ExceptionPanic(vector, frame);
            return;
        }

        PicEoi((int)(vector - PicBase)); // masked IRQ that still slipped through
    }

    // Handle an INT 21h from ring 3. Function number in AH, arguments in
    // RSI/RDI/..., result written back to RAX in the saved frame.
    private static void HandleSyscall(ulong* frame)
    {
        ulong rax = frame[14];           // full RAX as passed by the user
        ulong rsi = frame[10];           // arg0 (e.g. string pointer)
        ulong func = (rax >> 8) & 0xFF;  // AH = function number

        if (func == 0x09) // print '$'-terminated string at RSI
        {
            if (rsi < UserBase || rsi >= UserLimit)
            {
                frame[14] = unchecked((ulong)-1); // EFAULT
                return;
            }
            byte* p = (byte*)rsi;
            int n = 0;
            while (n < 4096 && p[n] != (byte)'$')
            {
                PutChar((char)p[n]);
                n++;
            }
            frame[14] = 0;
            return;
        }

        if (func == 0x4C) // terminate the program
        {
            WriteLine("[kernel] user program exited via INT 21h/AH=4Ch.");
            _tasks.state[TaskUser] = TaskDone;
            PickNext();          // switch away for good (never returns)
            for (;;) halt();
        }

        frame[14] = unchecked((ulong)-2); // ENOSYS
    }

    // A ring-3 program faulted (bad instruction, page fault, ...). Report it and
    // terminate just that task; the kernel and other tasks keep running.
    private static void UserFault(ulong vector, ulong* frame)
    {
        PutChar('\n');
        Write("[kernel] user program fault at CPL3: vector=");
        WriteHex(vector);
        Write(" err=");
        WriteHex(frame[16]);
        Write(" RIP=");
        WriteHex(frame[17]);
        PutChar('\n');
        WriteLine("[kernel] terminating the user task; system continues.");
        _tasks.state[TaskUser] = TaskDone;
        PickNext();              // switch away (never returns)
        for (;;) halt();
    }

    private static void ExceptionPanic(ulong vector, ulong* frame)
    {
        PutChar('\n');
        Write("!! CPU EXCEPTION vector=0x");
        WriteHex(vector);
        Write(" ERR=0x");
        WriteHex(frame[16]);
        Write(" RIP=0x");
        WriteHex(frame[17]);
        WriteLine("  -- system halted.");
        cli();
        for (;;) hlt_cpu();
    }

    // -------------------------------------------------- ring 3 / GDT + TSS
    // Build a GDT with kernel + user segments and a TSS descriptor, load it,
    // reload the data segment registers and load the task register. The TSS's
    // RSP0 points at the user task's kernel stack: the CPU switches to it when
    // an interrupt/syscall arrives from ring 3.
    private static void GdtInit()
    {
        // --- descriptor table ---
        for (int i = 0; i < 7 * 8; i++) _gdt.bytes[i] = 0;

        WriteGdtQuad(0x08, 0x00AF9A000000FFFF); // kernel code (DPL0)
        WriteGdtQuad(0x10, 0x00AF92000000FFFF); // kernel data (DPL0)
        WriteGdtQuad(0x18, 0x00AFFA000000FFFF); // user code   (DPL3)
        WriteGdtQuad(0x20, 0x00AFF2000000FFFF); // user data   (DPL3)

        // --- TSS descriptor at 0x28 (16 bytes, system type 0x9) ---
        ulong tssBase;
        fixed (byte* tp = _tss.bytes) { tssBase = (ulong)tp; }
        int o = 0x28;
        _gdt.bytes[o + 0] = 0x67; _gdt.bytes[o + 1] = 0x00;         // limit 0x0067
        _gdt.bytes[o + 2] = (byte)tssBase;
        _gdt.bytes[o + 3] = (byte)(tssBase >> 8);
        _gdt.bytes[o + 4] = (byte)(tssBase >> 16);
        _gdt.bytes[o + 5] = 0x89;                                    // present, type 0x9
        _gdt.bytes[o + 6] = 0x00;                                    // flags | limit hi
        _gdt.bytes[o + 7] = (byte)(tssBase >> 24);
        _gdt.bytes[o + 8]  = (byte)(tssBase >> 32);
        _gdt.bytes[o + 9]  = (byte)(tssBase >> 40);
        _gdt.bytes[o + 10] = (byte)(tssBase >> 48);
        _gdt.bytes[o + 11] = (byte)(tssBase >> 56);
        // bytes o+12..o+15 stay zero (reserved)

        // --- TSS body: RSP0 = user-task kernel stack top; no I/O bitmap ---
        for (int i = 0; i < 104; i++) _tss.bytes[i] = 0;
        ulong ktop;
        fixed (byte* p = _userKStack.bytes) { ktop = ((ulong)p + 16384) & ~(ulong)15; }
        fixed (byte* tp = _tss.bytes)
        {
            *(ulong*)(tp + 4) = ktop;                         // RSP0
            tp[100] = 0x68; tp[101] = 0x00;                   // IOPB = 104 (none)
        }

        // --- GDTR = { limit, base }, then load ---
        int limit = 7 * 8 - 1;
        ulong base_;
        fixed (byte* p = _gdt.bytes) { base_ = (ulong)p; }
        fixed (byte* p = _gdtr.data)
        {
            p[0] = (byte)limit; p[1] = (byte)(limit >> 8);
            p[2] = (byte)base_; p[3] = (byte)(base_ >> 8);
            p[4] = (byte)(base_ >> 16); p[5] = (byte)(base_ >> 24);
            p[6] = (byte)(base_ >> 32); p[7] = (byte)(base_ >> 40);
            p[8] = (byte)(base_ >> 48); p[9] = (byte)(base_ >> 56);
            osb_lgdt(p);
        }
        osb_reload_segments();
        osb_ltr(TssSel);
    }

    private static void WriteGdtQuad(int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
            _gdt.bytes[offset + i] = (byte)(value >> (i * 8));
    }

    // Copy the ring-3 test programs from the kernel image into the U/S=1 page.
    private static void LoadUserBlobs()
    {
        CopyBlob(osb_userstub_begin(), osb_userstub_end(), UserProgramAddr);
        CopyBlob(osb_userbad_begin(), osb_userbad_end(), UserBadAddr);
    }

    private static void CopyBlob(ulong src, ulong end, ulong dst)
    {
        byte* s = (byte*)src;
        byte* d = (byte*)dst;
        for (ulong i = 0; i < end - src; i++) d[i] = s[i];
    }

    // ------------------------------------------------- round-robin (preemptive)
    // Seed a task's kernel stack so the first switch into it lands at `entry`.
    // Layout mirrors what osb_switch pushes: [r15][r14][r13][r12][rbp][rbx][ret].
    private static ulong SeedTask(byte* stackBase, int size, ulong entry)
    {
        ulong top = (ulong)stackBase + (ulong)size;
        ulong sp = (top - 64) & ~(ulong)15;           // 16-byte aligned
        for (int i = 0; i < 6; i++) *(ulong*)(sp + (ulong)i * 8) = 0;
        *(ulong*)(sp + 48) = entry;
        return sp;
    }

    private static void SchedInit()
    {
        fixed (byte* p = _demoStack.bytes)
            _tasks.sp[TaskDemo] = SeedTask(p, 16384, osb_sched_get_demo_entry());
        fixed (byte* p = _userKStack.bytes)
            _tasks.sp[TaskUser] = SeedTask(p, 16384, osb_sched_get_user_entry());

        _tasks.sp[TaskShell] = 0;                     // set on the first switch out
        _tasks.state[TaskShell] = TaskReady;
        _tasks.state[TaskDemo] = TaskReady;
        _tasks.state[TaskUser] = TaskDone;            // started on demand via RING3
        _currentTask = TaskShell;
        _userEntry = UserProgramAddr;
        _demoDeadline = 0;
    }

    // Called from the IRQ0 handler on every tick. Round-robins to the next ready
    // task by swapping kernel stacks; the switch is atomic (interrupt gate) and
    // the interrupted ISR frame stays parked on the stopped task's stack.
    private static void SchedulerTick()
    {
        PickNext();
    }

    private static void PickNext()
    {
        int cur = _currentTask;
        int next = cur;
        for (int i = 0; i < TaskCount; i++)
        {
            next++;
            if (next >= TaskCount) next = 0;
            if (_tasks.state[next] == TaskReady) break;
        }
        if (next == cur) return;                      // only one runnable task

        _currentTask = next;
        fixed (ulong* sp = _tasks.sp)
            osb_switch(sp + cur, sp + next);
    }

    // Task 2 body: runs on the task's kernel stack; drops to ring 3 and never
    // comes back (the program exits via INT 21h or faults).
    [RuntimeExport("osb_user_task_main")]
    private static void UserTaskMain()
    {
        osb_enter_user(_userEntry, UserStackTop);
        for (;;) halt();                              // not reached
    }

    // Task 1: demo. Runs on its own stack, prints once per second and sleeps in
    // between. It never yields: the PIT preempts it.
    [RuntimeExport("osb_demo_task_main")]
    private static void DemoTaskMain()
    {
        for (;;)
        {
            if (_ticks >= _demoDeadline)
            {
                _demoCount++;
                Write("Demo task: round ");
                WriteDec(_demoCount);
                Write(" @ tick ");
                WriteDec(_ticks);
                WriteLine("");
                _demoDeadline = _ticks + (ulong)PitHz;   // next round in 1 s
            }
            halt(); // sleep until the next interrupt; the PIT schedules the shell
        }
    }

    // ---------------------------------------------------------------- keyboard
    private static char MapScancode(byte sc, bool shift)
    {
        switch (sc)
        {
            case 0x02: return shift ? '!' : '1';
            case 0x03: return shift ? '@' : '2';
            case 0x04: return shift ? '#' : '3';
            case 0x05: return shift ? '$' : '4';
            case 0x06: return shift ? '%' : '5';
            case 0x07: return shift ? '^' : '6';
            case 0x08: return shift ? '&' : '7';
            case 0x09: return shift ? '*' : '8';
            case 0x0A: return shift ? '(' : '9';
            case 0x0B: return shift ? ')' : '0';
            case 0x0C: return shift ? '_' : '-';
            case 0x0D: return shift ? '+' : '=';
            case 0x10: return 'Q';
            case 0x11: return 'W';
            case 0x12: return 'E';
            case 0x13: return 'R';
            case 0x14: return 'T';
            case 0x15: return 'Y';
            case 0x16: return 'U';
            case 0x17: return 'I';
            case 0x18: return 'O';
            case 0x19: return 'P';
            case 0x1A: return shift ? '{' : '[';
            case 0x1B: return shift ? '}' : ']';
            case 0x1E: return 'A';
            case 0x1F: return 'S';
            case 0x20: return 'D';
            case 0x21: return 'F';
            case 0x22: return 'G';
            case 0x23: return 'H';
            case 0x24: return 'J';
            case 0x25: return 'K';
            case 0x26: return 'L';
            case 0x27: return shift ? ':' : ';';
            case 0x28: return shift ? '"' : '\'';
            case 0x29: return shift ? '~' : '`';
            case 0x2B: return shift ? '|' : '\\';
            case 0x2C: return 'Z';
            case 0x2D: return 'X';
            case 0x2E: return 'C';
            case 0x2F: return 'V';
            case 0x30: return 'B';
            case 0x31: return 'N';
            case 0x32: return 'M';
            case 0x33: return shift ? '<' : ',';
            case 0x34: return shift ? '>' : '.';
            case 0x35: return shift ? '?' : '/';
            case 0x39: return ' ';
            default: return (char)0;
        }
    }

    // ---------------------------------------------------------------- shell
    private static void ShellLoop()
    {
        WriteLine("OSB> ");
        char* line = stackalloc char[128];
        int len = 0;
        bool shift = false;

        while (true)
        {
            // Drain the IRQ1 ring buffer. When empty, sleep: the PIT handler
            // preempts us and schedules the demo task (preemptive round-robin).
            int sc = KbdPop();
            if (sc < 0) { halt(); continue; }

            if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
            if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
            if ((sc & 0x80) != 0) continue; // ignore other break codes

            if (sc == 0x1C) // Enter
            {
                PutChar('\n');
                line[len] = '\0';
                ExecuteLine(line, len);
                len = 0;
                Write("OSB> ");
                continue;
            }

            if (sc == 0x0E) // Backspace
            {
                if (len > 0)
                {
                    len--;
                    PutChar('\b');
                    PutChar(' ');
                    PutChar('\b');
                }
                continue;
            }

            char c = MapScancode((byte)sc, shift);
            if (c != 0 && len < 127)
            {
                line[len++] = c;
                PutChar(c);
            }
        }
    }

    private static void ExecuteLine(char* line, int len)
    {
        if (len == 0) return;

        if (Eq(line, len, "VER")) { WriteLine("OSB (dotnet) version 0.1 - bootable kernel"); return; }
        if (Eq(line, len, "ABOUT")) { WriteLine("OSB: Operating System Basic, created in 2000, rebooted in C#."); return; }
        if (Eq(line, len, "HELP"))
        {
            WriteLine("Commands: VER  ABOUT  HELP  CLS  ECHO <text>  TICKS");
            WriteLine("          SLEEP <seconds>  TIME  DATE");
            WriteLine("          RING3 [BAD]  (run a ring-3 program)");
            WriteLine("          HALT  POWEROFF  REBOOT");
            // note: ECHO is handled below
            return;
        }
        if (Eq(line, len, "CLS")) { VgaClear(); return; }
        if (Eq(line, len, "RING3") || StartsWith(line, len, "RING3 "))
        {
            bool bad = false;
            int i = 5;
            while (i < len && line[i] == ' ') i++;
            if (i + 2 < len && Up(line[i]) == 'B' && Up(line[i + 1]) == 'A' && Up(line[i + 2]) == 'D')
                bad = true;
            LaunchUserProgram(bad);
            return;
        }
        if (Eq(line, len, "HALT"))
        {
            WriteLine("System halted. (CPU stopped; in QEMU press Ctrl+Alt+2 then");
            WriteLine("'quit', or use POWEROFF to close the emulator cleanly.)");
            cli();
            for (;;) hlt_cpu();
        }
        if (Eq(line, len, "POWEROFF") || Eq(line, len, "SHUTDOWN") ||
            Eq(line, len, "EXIT") || Eq(line, len, "QUIT"))
        {
            WriteLine("Powering off (ACPI S5) ...");
            cli();
            osb_poweroff();                 // QEMU exits; on real HW this is S5
            for (;;) hlt_cpu();             // fallback if ACPI is unavailable
        }
        if (Eq(line, len, "REBOOT"))
        {
            WriteLine("Rebooting ...");
            osb_reboot();                   // 8042 reset; never returns
            for (;;) hlt_cpu();
        }
        if (Eq(line, len, "TIME"))
        {
            WriteTwo(_rtcHour); PutChar(':');
            WriteTwo(_rtcMin); PutChar(':');
            WriteTwo(_rtcSec); PutChar('\n');
            return;
        }
        if (Eq(line, len, "DATE"))
        {
            WriteTwo(_rtcDay); PutChar('/');
            WriteTwo(_rtcMonth); PutChar('/');
            WriteDec(2000UL + _rtcYear);
            PutChar('\n');
            return;
        }
        if (Eq(line, len, "TICKS"))
        {
            Write("Uptime: ");
            WriteDec(_ticks / (ulong)PitHz);
            Write(" s (");
            WriteDec(_ticks);
            WriteLine(" ticks)");
            return;
        }
        if (StartsWith(line, len, "SLEEP"))
        {
            int i = 5;
            while (i < len && line[i] == ' ') i++;
            ulong secs = ParseUInt(line, i, len);
            WriteLine("Sleeping ...");
            ulong target = _ticks + secs * (ulong)PitHz;
            while (_ticks < target) halt(); // tick-driven wait; keys queue up
            WriteLine("Done.");
            return;
        }
        if (StartsWith(line, len, "ECHO"))
        {
            int i = 4;
            while (i < len && line[i] == ' ') i++;
            for (; i < len; i++)
                PutChar(line[i]);
            PutChar('\n');
            return;
        }

        WriteLine("Bad command or file name.");
    }

    private static bool Eq(char* line, int len, string lit)
    {
        if (len != lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < len; i++)
            {
                char a = line[i];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (a != p[i]) return false;
            }
        }
        return true;
    }

    private static bool StartsWith(char* line, int len, string lit)
    {
        if (len < lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < lit.Length; i++)
            {
                char a = line[i];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (a != p[i]) return false;
            }
        }
        return true;
    }

    private static char Up(char c)
    {
        return (c >= 'a' && c <= 'z') ? (char)(c - 32) : c;
    }

    // Re-seed the ring-3 task and mark it runnable; the PIT will schedule it and
    // it will drop to user mode on its own (multitasking preserved).
    private static void LaunchUserProgram(bool bad)
    {
        _userEntry = bad ? UserBadAddr : UserProgramAddr;
        fixed (byte* p = _userKStack.bytes)
            _tasks.sp[TaskUser] = SeedTask(p, 16384, osb_sched_get_user_entry());
        _tasks.state[TaskUser] = TaskReady;

        WriteLine(bad
            ? "Launching ring-3 program (supervisor-escape test) ..."
            : "Launching ring-3 program ...");
    }
}
