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
    private const int KbdVector = 32 + 1;          // IRQ1 remapped to vector 33
    private const int TimerVector = 32 + 0;        // IRQ0 (PIT) -> vector 32
    private const int PitHz = 100;                 // PIT frequency in Hz

    private unsafe struct IdtTable { public fixed byte bytes[256 * 16]; }
    private unsafe struct IdtrTable { public fixed byte data[10]; } // lidt operand
    private unsafe struct KbdRing
    {
        public fixed byte data[128];
        public int head;
        public int tail;
    }

    private static IdtTable _idt;
    private static IdtrTable _idtr;
    private static KbdRing _kbd;
    private static ulong _ticks;                    // 100 Hz heartbeat

    // Entry point (named "Main" so the assembly has a managed entry point; the
    // real entry is kmain, called by _start in boot.S).
    private static void Main() { }

    [RuntimeExport("kmain")]
    private static void KmMain(ulong multibootInfo)
    {
        SerialInit();
        VgaClear();

        PicInit();          // remap 8259A (IRQ0-15 -> vectors 32-47), unmask IRQ0+IRQ1
        PitInit();          // 8254 channel 0: square wave @ 100 Hz
        IdtInit();          // fill + load the IDT (exception + IRQ stubs)

        WriteLine("OSB - Operating System Basic");
        WriteLine("Bootable kernel prototype (C# / NativeAOT, bare metal)");
        WriteLine("Keyboard: PS/2 IRQ1 via IDT + PIC (interrupt-driven).");
        WriteLine("Timer: PIT channel 0 @ 100 Hz (IRQ0 ticks).");
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
        ulong table = osb_get_stub_table();
        for (int i = 0; i < 256; i++)
            IdtWriteEntry(i, *(ulong*)(table + (ulong)i * 8), IdtInterruptGate);

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

    // 8259A: remap IRQ0-7 -> 0x20-0x27 and IRQ8-15 -> 0x28-0x2F (8086 mode,
    // slave on IRQ2), then mask everything except IRQ0 (timer) and IRQ1 (keyboard).
    private static void PicInit()
    {
        outb(0x20, 0x11); outb(0x80, 0);  // ICW1: init, edge triggered
        outb(0xA0, 0x11); outb(0x80, 0);
        outb(0x21, 0x20); outb(0x80, 0);  // ICW2: master base vector 0x20
        outb(0xA1, 0x28); outb(0x80, 0);  // ICW2: slave base vector 0x28
        outb(0x21, 0x04); outb(0x80, 0);  // ICW3: slave on master IRQ2
        outb(0xA1, 0x02); outb(0x80, 0);
        outb(0x21, 0x01); outb(0x80, 0);  // ICW4: 8086/8088 mode
        outb(0xA1, 0x01); outb(0x80, 0);
        outb(0x21, 0xFC); outb(0x80, 0);  // OCW1: unmask IRQ0 (timer) + IRQ1 (kbd)
        outb(0xA1, 0xFF); outb(0x80, 0);  // OCW1: mask all slave IRQs
    }

    // 8254 PIT, channel 0: 1193182 Hz / divisor -> ~100 Hz square wave on IRQ0.
    private static void PitInit()
    {
        uint divisor = (uint)(1193182 / PitHz); // 11931 ~= 100.000 Hz
        outb(0x43, 0x36);                      // ch0, lobyte/hibyte, mode 3, binary
        outb(0x40, (byte)(divisor & 0xFF));
        outb(0x40, (byte)(divisor >> 8));
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
            PicEoi(0);
            return;
        }

        if (vector < 32)
        {
            ExceptionPanic(vector, frame); // fatal CPU exception
            return;
        }

        PicEoi((int)(vector - 32)); // masked IRQ that still slipped through
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
            // Drain the IRQ1 ring buffer. When empty, sleep until the next
            // keyboard interrupt instead of polling the controller.
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
            WriteLine("          SLEEP <seconds>  HALT");
            // note: ECHO is handled below
            return;
        }
        if (Eq(line, len, "CLS")) { VgaClear(); return; }
        if (Eq(line, len, "HALT")) { WriteLine("System halted."); cli(); for (;;) hlt_cpu(); }
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
}
