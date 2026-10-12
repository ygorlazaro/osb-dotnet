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
    [DllImport("osbnative")] private static extern void outw(ushort port, ushort value);
    [DllImport("osbnative")] private static extern byte inb(ushort port);
    [DllImport("osbnative")] private static extern ushort inw(ushort port);
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
    // per-process address spaces (boot.S)
    [DllImport("osbnative")] private static extern void osb_load_cr3(ulong cr3);
    [DllImport("osbnative")] private static extern ulong osb_master_pml4();
    [DllImport("osbnative")] private static extern ulong osb_master_pd();
    // user-blob bounds (user.S)
    [DllImport("osbnative")] private static extern ulong osb_userstub_begin();
    [DllImport("osbnative")] private static extern ulong osb_userstub_end();
    [DllImport("osbnative")] private static extern ulong osb_userbad_begin();
    [DllImport("osbnative")] private static extern ulong osb_userbad_end();
    [DllImport("osbnative")] private static extern ulong osb_spawner_begin();
    [DllImport("osbnative")] private static extern ulong osb_spawner_end();

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
    private const int SerVector = PicBase + 4;     // IRQ4 -> vector 0x34 (COM1 RX)
    private const int RtcVector = PicBase + 8;     // IRQ8 -> vector 0x38
    private const int Int21Vector = 0x21;          // DOS-style syscall gate
    private const int PitHz = 100;                 // PIT frequency in Hz

    // ---- ring 3 / user memory layout ----
    // Every process has the same user virtual layout (code at 0x400000, stack
    // just below 0x480000) but a *private* 2 MiB physical block behind it, so
    // one process cannot see another's memory. The block is mapped at pd[2].
    private const ulong UserBase = 0x400000;       // user code/data region start
    private const ulong UserLimit = 0x600000;      // end of the U/S=1 2 MiB page
    private const ulong UserStackTop = 0x480000;   // user stack grows down from here

    // ---- physical memory (frame allocator) ----
    private const ulong PhysFloorDefault = 0x200000;   // just above the kernel image
    private const ulong PhysTopDefault = 0x4000000;    // 64 MiB (QEMU -m 64M)
    private const int FrameSize = 4096;
    private const int BlockFrames = 512;               // 2 MiB user block
    private const ulong BlockAlign = 0x200000;
    // Frames are never handed out from [0x400000,0x600000): that 2 MiB window is
    // pd[2] in every address space, so the kernel cannot address a fresh block
    // through its physical address there (VA != phys inside the user window).
    private const ulong ReservedLo = 0x400000;
    private const ulong ReservedHi = 0x600000;

    // GDT selectors (64-bit flat, base 0)
    private const ushort KernelDataSel = 0x10;
    private const ushort UserCodeSel = 0x18;       // RPL3 -> 0x1B in CS
    private const ushort UserDataSel = 0x20;       // RPL3 -> 0x23 in SS/DS
    private const ushort TssSel = 0x28;

    private const byte RtcIndex = 0x70;            // CMOS register select
    private const byte RtcData = 0x71;             // CMOS data port

    // ---- ATA PIO (IDE) ----
    // Primary IDE: data 0x1F0, error/features 0x1F1, sector count 0x1F2,
    // LBA low/mid/high 0x1F3-0x1F5, drive/head 0x1F6, status/command 0x1F7
    private const ushort AtaPrimaryData = 0x1F0;
    private const ushort AtaPrimaryError = 0x1F1;
    private const ushort AtaPrimarySecCount = 0x1F2;
    private const ushort AtaPrimaryLbaLow = 0x1F3;
    private const ushort AtaPrimaryLbaMid = 0x1F4;
    private const ushort AtaPrimaryLbaHigh = 0x1F5;
    private const ushort AtaPrimaryDriveHead = 0x1F6;
    private const ushort AtaPrimaryStatus = 0x1F7;
    private const ushort AtaPrimaryCommand = 0x1F7;
    private const byte AtaCmdReadSectors = 0x20;
    private const byte AtaCmdWriteSectors = 0x30;
    private const byte AtaCmdIdentify = 0xEC;
    private const byte AtaStatusBusy = 0x80;
    private const byte AtaStatusDrq = 0x08;
    private const byte AtaStatusReady = 0x40;

    private unsafe struct IdtTable { public fixed byte bytes[256 * 16]; }
    private unsafe struct IdtrTable { public fixed byte data[10]; } // lidt operand
    private unsafe struct KbdRing
    {
        public fixed byte data[128];
        public int head;
        public int tail;
    }
    private unsafe struct CharRing
    {
        public fixed byte data[256];
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
    private static CharRing _ser;                   // serial console input (COM1 RX)
    private static ulong _ticks;                    // 100 Hz heartbeat
    private static bool _ctrlC;                     // Ctrl+C was pressed (consumed by the shell)
    private static bool _kbdCtrl;                   // PS/2 Ctrl is currently held down

    // Software clock maintained by the RTC "update-ended" interrupt (IRQ8).
    private static byte _rtcSec, _rtcMin, _rtcHour, _rtcDay, _rtcMonth, _rtcYear;

    // ---- environment ($VAR, case-insensitive names) ----
    // Fixed buffers (no managed arrays: the freestanding AOT runtime has no
    // bounds-check helper, so the kernel uses fixed-size tables like the
    // scheduler's TaskTable).
    private const int MaxEnvVars = 32;
    private const int EnvNameBufSize = 512;
    private const int EnvValueBufSize = 4096;
    private unsafe struct EnvTable
    {
        public fixed int nameOff[MaxEnvVars];
        public fixed int nameLen[MaxEnvVars];
        public fixed int valueOff[MaxEnvVars];
        public fixed int valueLen[MaxEnvVars];
        public fixed byte names[EnvNameBufSize];
        public fixed byte values[EnvValueBufSize];
    }
    private static EnvTable _env;
    private static int _envCount;
    private static int _envNamePos;
    private static int _envValuePos;

    // DOS-like namespace state (drive A:, current directory). The ext4 layer
    // updates this when CD is implemented; the boot value is the root.
    private static char _curDrive = 'A';
    private unsafe struct CwdState { public fixed byte path[160]; }
    private static CwdState _cwd;
    private static int _cwdALen;


    // Preemptive round-robin scheduler over a fixed table of task slots.
    // Slot 0 is the console/shell (PID 1, kernel address space); the others are
    // processes/jobs, each with its own kernel stack. A ring-3 process also owns
    // a private address space (CR3) and a 2 MiB user block.
    private const int SlotConsole = 0;             // the shell / console (PID 1)
    private const int TaskCount = 8;               // console + 7 process slots
    private const int TaskFree = 0;                // slot not in use
    private const int TaskReady = 1;               // runnable (or running)
    private const int TaskZombie = 2;              // exited, not yet reaped
    private const int KindNone = 0;
    private const int KindShell = 1;
    private const int KindDemo = 2;
    private const int KindUser = 3;                // runs in ring 3
    private const int StackSize = 16384;
    private const int MaxName = 12;

    // Built-in ring-3 programs (no filesystem yet).
    private const int ProgHello = 0;
    private const int ProgBad = 1;
    private const int ProgSpawner = 2;             // exercises INT 21h AH=80h/81h

    private unsafe struct TaskTable
    {
        public fixed ulong sp[TaskCount];          // saved kernel SP (osb_switch)
        public fixed int pid[TaskCount];
        public fixed int ppid[TaskCount];
        public fixed int state[TaskCount];
        public fixed int kind[TaskCount];
        public fixed int fg[TaskCount];            // 1 = foreground job
        public fixed int exitCode[TaskCount];
        public fixed ulong arg[TaskCount];         // kind-specific (user entry RIP)
        public fixed ulong cr3[TaskCount];         // address space (page table root)
        public fixed ulong ublock[TaskCount];      // 2 MiB user block (phys), 0 if none
        public fixed ulong ptphys[TaskCount];      // 3 page-table frames (phys), 0 if none
        public fixed ulong round[TaskCount];       // demo: rounds printed
        public fixed ulong deadline[TaskCount];    // demo: next print tick
        public fixed byte name[TaskCount * MaxName];
    }
    private unsafe struct StackPool { public fixed byte bytes[TaskCount * StackSize]; }

    // Physical frame bitmap: covers up to 1 GiB at 4 KiB granularity.
    private const int MaxFrames = 262144;          // 1 GiB / 4 KiB
    private unsafe struct FrameMap { public fixed byte bits[MaxFrames / 8]; }

    private static TaskTable _tasks;
    private static StackPool _stacks;
    private static FrameMap _frames;
    private static int _currentTask;               // slot currently on the CPU
    private static int _nextPid;                   // next pid to hand out
    private static int _fgSlot;                    // foreground job slot, or -1
    private static ulong _physFloor;               // first allocatable physical address
    private static ulong _physTop;                 // end of allocatable RAM
    private static ulong _frameTotal;              // allocatable frame count
    private static ulong _frameUsed;               // allocated frame count

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
        ParseMmap(multibootInfo);   // find how much RAM we can hand out
        FramesInit();       // physical frame allocator (bitmap)
        IdtInit();          // fill + load the IDT (exception + IRQ stubs)
        SchedInit();        // console (slot 0) + kernel address space

        WriteLine("OSB - Operating System Basic");
        WriteLine("Bootable kernel prototype (C# / NativeAOT, bare metal)");
        WriteLine("Keyboard: PS/2 IRQ1 + serial console IRQ4 (COM1).");
        WriteLine("Timer: PIT channel 0 @ 100 Hz (IRQ0 ticks).");
        WriteLine("Clock: RTC via IRQ8 (update-ended).");
        WriteLine("Scheduler: preemptive round-robin over a task table.");
        WriteLine("Processes: private address space per process (CR3).");
        WriteLine("Ring 3: user segments + TSS; syscalls via INT 21h (DOS-style).");
        WriteLine("Jobs: DEMO / SPAWN / RING3; TASK / KILL / TAB / WAIT.");
        WriteLine("Files: ATA PIO + ext4 on A: (DIR / CD / TYPE)");
        WriteLine("Type HELP for commands.");
        WriteLine("");

        EnvInit();          // session environment (PATH, PROMPT, USER, ...)
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

    private static void AtaWaitBusy()
    {
        while ((inb(AtaPrimaryStatus) & AtaStatusBusy) != 0) { }
    }

    private static void AtaWaitDrq()
    {
        while ((inb(AtaPrimaryStatus) & AtaStatusDrq) == 0) { }
    }

    private static unsafe void AtaReadSector(ulong lba, byte* buffer)
    {
        AtaWaitBusy();
        outb(AtaPrimaryDriveHead, (byte)(0xE0 | (byte)((lba >> 24) & 0x0F)));
        outb(AtaPrimarySecCount, 1);
        outb(AtaPrimaryLbaLow, (byte)(lba & 0xFF));
        outb(AtaPrimaryLbaMid, (byte)((lba >> 8) & 0xFF));
        outb(AtaPrimaryLbaHigh, (byte)((lba >> 16) & 0xFF));
        outb(AtaPrimaryCommand, AtaCmdReadSectors);
        AtaWaitBusy();
        AtaWaitDrq();
        for (int i = 0; i < 256; i++)
            ((ushort*)buffer)[i] = inw(AtaPrimaryData);
    }

// ------------------------------------------------------------ ATA write
    private static unsafe void AtaWriteSector(ulong lba, byte* buffer)
    {
        AtaWaitBusy();
        outb(AtaPrimaryDriveHead, (byte)(0xE0 | (byte)((lba >> 24) & 0x0F)));
        outb(AtaPrimarySecCount, 1);
        outb(AtaPrimaryLbaLow, (byte)(lba & 0xFF));
        outb(AtaPrimaryLbaMid, (byte)((lba >> 8) & 0xFF));
        outb(AtaPrimaryLbaHigh, (byte)((lba >> 16) & 0xFF));
        outb(AtaPrimaryCommand, AtaCmdWriteSectors);
        AtaWaitBusy();
        AtaWaitDrq();
        for (int i = 0; i < 256; i++)
            outw(AtaPrimaryData, ((ushort*)buffer)[i]);
        AtaWaitBusy();
    }

    // ------------------------------------------------------------- FS: ext4    // ------------------------------------------------------------- FS: ext4
    // Simplified ext4 (as formatted by build.sh): 4096-byte blocks, no
    // journal, no extents, no flex_bg, no metadata_csum, case-sensitive.
    // One group; primary superblock at byte 1024 of block 0, GDT at block 1.
    private const int Ext4BlockSize = 4096;

    private static unsafe void FsReadBlock(ulong block, byte* buf)
    {
        ulong lba = block * 8UL;
        for (int i = 0; i < 8; i++) AtaReadSector(lba + (ulong)i, buf + (i * 512));
    }

    private static unsafe void FsWriteBlock(ulong block, byte* buf)
    {
        ulong lba = block * 8UL;
        for (int i = 0; i < 8; i++) AtaWriteSector(lba + (ulong)i, buf + (i * 512));
    }

    // little-endian 16/32-bit accessors
    private static unsafe ushort Rd16(byte* p, int o) { return (ushort)(p[o] | (p[o + 1] << 8)); }
    private static unsafe uint Rd32(byte* p, int o) { return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)); }
    private static unsafe void Wr16(byte* p, int o, ushort v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)(v >> 8); }
    private static unsafe void Wr32(byte* p, int o, uint v) { p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)((v >> 8) & 0xFF); p[o + 2] = (byte)((v >> 16) & 0xFF); p[o + 3] = (byte)((v >> 24) & 0xFF); }

    // superblock (sb points at block-bytes + 1024)
    private static unsafe ushort SbMagic(byte* sb) { return Rd16(sb, 56); }
    private static unsafe ushort SbInodeSize(byte* sb) { ushort r = Rd16(sb, 88); return r >= 128 ? r : (ushort)128; }
    private static unsafe uint SbInodesPerGroup(byte* sb) { return Rd32(sb, 40); }
    private static unsafe uint SbBlocksPerGroup(byte* sb) { return Rd32(sb, 32); }
    private static unsafe uint SbBlocksCount(byte* sb) { return Rd32(sb, 4); }

    // group descriptor entry (32 bytes each)
    private static unsafe uint GdBlockBitmap(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32); }
    private static unsafe uint GdInodeBitmap(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32 + 4); }
    private static unsafe uint GdInodeTable(byte* gdt, uint group) { return Rd32(gdt, (int)group * 32 + 8); }

    // fs state (filled by FsMountA)
    private static bool _fsMounted;
    private static int _inodeSize = 256;
    private static uint _inodesPerGroup, _blocksPerGroup, _groupCount;
    private static uint _blkBitmapBlock, _inoBitmapBlock, _inoTableBlock;
    private static readonly uint _rootInode = 2;

    private static unsafe bool FsMountA()
    {
        if (_fsMounted) return true;
        byte* sbuf = stackalloc byte[Ext4BlockSize];
        FsReadBlock(0UL, sbuf);
        byte* sb = sbuf + 1024;
        if (SbMagic(sb) != 0xEF53) return false;
        _inodeSize = SbInodeSize(sb);
        _inodesPerGroup = SbInodesPerGroup(sb);
        _blocksPerGroup = SbBlocksPerGroup(sb);
        uint blocks = SbBlocksCount(sb);
        _groupCount = (blocks + _blocksPerGroup - 1) / _blocksPerGroup;
        if (_groupCount == 0) _groupCount = 1;
        byte* gdt = stackalloc byte[Ext4BlockSize];
        FsReadBlock(1UL, gdt);
        _blkBitmapBlock = GdBlockBitmap(gdt, 0);
        _inoBitmapBlock = GdInodeBitmap(gdt, 0);
        _inoTableBlock = GdInodeTable(gdt, 0);
        _fsMounted = true;
        return true;
    }

    // ---- inode table (contiguous blocks; an inode spans at most 2 blocks) ----
    private static unsafe bool FsReadInode(uint ino, byte* outBuf)
    {
        if (!_fsMounted || ino == 0) return false;
        ulong abs = (ulong)_inoTableBlock * (ulong)Ext4BlockSize + (ulong)(ino - 1) * (ulong)_inodeSize;
        byte* blk0 = stackalloc byte[Ext4BlockSize];
        FsReadBlock(abs / (ulong)Ext4BlockSize, blk0);
        ulong off0 = abs % (ulong)Ext4BlockSize;
        ulong first = (ulong)Ext4BlockSize - off0;
        if (first > (ulong)_inodeSize) first = (ulong)_inodeSize;
        for (ulong i = 0; i < first; i++) outBuf[i] = blk0[off0 + i];
        if (first < (ulong)_inodeSize)
        {
            byte* blk1 = stackalloc byte[Ext4BlockSize];
            FsReadBlock((abs + first) / (ulong)Ext4BlockSize, blk1);
            for (ulong i = first; i < (ulong)_inodeSize; i++) outBuf[i] = blk1[(off0 + i) - (ulong)Ext4BlockSize];
        }
        return true;
    }

    private static unsafe bool FsWriteInode(uint ino, byte* inBuf)
    {
        if (!_fsMounted || ino == 0) return false;
        ulong abs = (ulong)_inoTableBlock * (ulong)Ext4BlockSize + (ulong)(ino - 1) * (ulong)_inodeSize;
        byte* blk0 = stackalloc byte[Ext4BlockSize];
        FsReadBlock(abs / (ulong)Ext4BlockSize, blk0);
        ulong off0 = abs % (ulong)Ext4BlockSize;
        ulong first = (ulong)Ext4BlockSize - off0;
        if (first > (ulong)_inodeSize) first = (ulong)_inodeSize;
        for (ulong i = 0; i < first; i++) blk0[off0 + i] = inBuf[i];
        FsWriteBlock(abs / (ulong)Ext4BlockSize, blk0);
        if (first < (ulong)_inodeSize)
        {
            byte* blk1 = stackalloc byte[Ext4BlockSize];
            FsReadBlock((abs + first) / (ulong)Ext4BlockSize, blk1);
            for (ulong i = first; i < (ulong)_inodeSize; i++) blk1[(off0 + i) - (ulong)Ext4BlockSize] = inBuf[i];
            FsWriteBlock((abs + first) / (ulong)Ext4BlockSize, blk1);
        }
        return true;
    }

    // ---- inode fields ----
    private const int InoMode = 0, InoUid = 2, InoSizeLo = 4, InoMtime = 16,
                      InoGid = 24, InoLinks = 26, InoBlocksLo = 28, InoFlags = 32,
                      InoBlock = 40, InoSizeHi = 108, InoBlocksHi = 116;

    private static unsafe ushort InoGetMode(byte* n) { return Rd16(n, InoMode); }
    private static unsafe uint InoGetUid(byte* n) { return Rd16(n, InoUid); }
    private static unsafe uint InoGetGid(byte* n) { return Rd16(n, InoGid); }
    private static unsafe uint InoGetLinks(byte* n) { return Rd16(n, InoLinks); }
    private static unsafe uint InoGetSize(byte* n)
    {
        ulong hi = Rd32(n, InoSizeHi);
        ulong lo = Rd32(n, InoSizeLo);
        ulong sz = (hi << 32) | lo;
        return sz > 0xFFFFFFFFUL ? 0xFFFFFFFFU : (uint)sz;
    }
    private static unsafe uint InoGetBlocks(byte* n)
    {
        ulong lo = Rd32(n, InoBlocksLo);
        ulong hi = (ulong)Rd16(n, InoBlocksHi);
        ulong b = lo | (hi << 32);
        return b > 0xFFFFFFFFUL ? 0xFFFFFFFFU : (uint)b;
    }
    private static unsafe uint InoGetBlockPtr(byte* n, int idx) { return (idx < 0 || idx > 14) ? 0U : Rd32(n, InoBlock + idx * 4); }

    private static bool FsIsDir(ushort mode) { return (mode & 0xF000) == 0x4000; }
    private static bool FsIsReg(ushort mode) { return (mode & 0xF000) == 0x8000; }

    // logical -> physical block: 12 direct + single indirect
    private static unsafe uint FsInodeGetBlock(byte* inode, ulong logical)
    {
        if (logical < 12) return InoGetBlockPtr(inode, (int)logical);
        if (logical < 12 + 1024)
        {
            uint ib = InoGetBlockPtr(inode, 12);
            if (ib == 0) return 0;
            byte* ibuf = stackalloc byte[Ext4BlockSize];
            FsReadBlock(ib, ibuf);
            return Rd32(ibuf, (int)(logical - 12) * 4);
        }
        return 0; // double indirect: not needed for the OSB dev image
    }

    // ---- ext4 directory entries (ext4_dir_entry_2) ----
    private const int DirIno = 0, DirRecLen = 4, DirNameLen = 6, DirType = 7, DirName = 8;
    private static unsafe uint DirGetIno(byte* de) { return Rd32(de, DirIno); }
    private static unsafe ushort DirGetRecLen(byte* de) { return Rd16(de, DirRecLen); }
    private static unsafe byte DirGetNameLen(byte* de) { return de[DirNameLen]; }
    private static unsafe byte DirGetType(byte* de) { return de[DirType]; }

    // Case-sensitive child lookup: name must match exactly.
    private static unsafe bool FsFindInDir(byte* dirIno, byte* name, int nameLen, out uint childIno, out byte childType)
    {
        childIno = 0;
        childType = 0;
        if (!FsIsDir(InoGetMode(dirIno))) return false;
        uint size = InoGetSize(dirIno);
        ulong blocks = ((ulong)size + (ulong)Ext4BlockSize - 1) / (ulong)Ext4BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[Ext4BlockSize];
        for (ulong b = 0; b < blocks; b++)
        {
            uint bnum = FsInodeGetBlock(dirIno, b);
            if (bnum == 0) continue;
            FsReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= Ext4BlockSize)
            {
                byte* de = blk + off;
                uint iino = DirGetIno(de);
                ushort rlen = DirGetRecLen(de);
                if (rlen == 0) break;
                byte nlen = DirGetNameLen(de);
                if (iino != 0 && nlen == (byte)nameLen)
                {
                    bool same = true;
                    for (int k = 0; k < nameLen; k++)
                        if (de[DirName + k] != name[k]) { same = false; break; }
                    if (same) { childIno = iino; childType = DirGetType(de); return true; }
                }
                off += rlen;
                if (off > Ext4BlockSize - 8) break;
            }
        }
        return false;
    }

    // Resolves "A:\OSB\BIN", "\OSB", "OSB", "A:OSB", "." and ".." using the
    // current directory of drive A: (_cwd). Returns the inode and builds the
    // canonical absolute path ("A:\OSB\BIN") in canon.
    private static unsafe bool FsResolveCanon(char* path, int len, out uint ino, byte* canon, ref int canonLen)
    {
        ino = 0;
        if (!_fsMounted && !FsMountA()) return false;
        byte* abs = stackalloc byte[300];
        int al = 0;
        int i = 0;
        bool absolute = false;
        // drive prefix
        if (len >= 2 && path[1] == ':')
        {
            char d = path[0];
            if (d >= 'a' && d <= 'z') d = (char)(d - 32);
            if (d != 'A') { WriteLine("Drive not ready."); return false; }
            i = 2;
            while (i < len && (path[i] == '\\' || path[i] == '/')) { i++; absolute = true; }
        }
        if (!absolute && len >= 1 && (path[0] == '\\' || path[0] == '/'))
        {
            i = 1;
            absolute = true;
        }
        if (absolute)
        {
            abs[0] = (byte)'A'; abs[1] = (byte)':'; abs[2] = (byte)'\\'; al = 3;
        }
        else
        {
            CwdInit();
            for (int k = 0; k < _cwdALen && al < 299; k++) abs[al++] = _cwd.path[k];
        }
        // append the input segments
        while (i < len)
        {
            while (i < len && (path[i] == '\\' || path[i] == '/' || path[i] == ' ')) i++;
            int s = i;
            while (i < len && path[i] != '\\' && path[i] != '/') i++;
            int segLen = i - s;
            if (segLen > 0)
            {
                if (al > 0 && abs[al - 1] != '\\') abs[al++] = (byte)'\\';
                if (al + segLen > 299) return false;
                for (int k = 0; k < segLen; k++) abs[al++] = (byte)path[s + k];
            }
        }

        // walk the segments from the root
        byte* din = stackalloc byte[256];
        ino = _rootInode;
        uint cl = 0;
        canon[cl++] = (byte)'A'; canon[cl++] = (byte)':'; canon[cl++] = (byte)'\\';
        int pos = 3;
        while (pos < al)
        {
            while (pos < al && (abs[pos] == (byte)'\\' || abs[pos] == (byte)'/')) pos++;
            int s = pos;
            while (pos < al && abs[pos] != (byte)'\\' && abs[pos] != (byte)'/') pos++;
            int segLen = pos - s;
            if (segLen <= 0) continue;
            if (segLen == 1 && abs[s] == (byte)'.') continue;
            if (segLen == 2 && abs[s] == (byte)'.' && abs[s + 1] == (byte)'.')
            {
                // parent: shrink canon to the previous separator
                if (cl > 3)
                {
                    cl--;
                    while (cl > 3 && canon[cl - 1] != (byte)'\\') cl--;
                }
                // resolve parent inode via the ".." entry
                if (!FsReadInode(ino, din)) return false;
                byte* dd = stackalloc byte[2];
                dd[0] = (byte)'.'; dd[1] = (byte)'.';
                uint pino; byte pt;
                if (!FsFindInDir(din, dd, 2, out pino, out pt)) return false;
                ino = pino;
                continue;
            }
            // normal segment: look it up in the current directory
            if (!FsReadInode(ino, din)) return false;
            uint cino;
            byte ct;
            if (!FsFindInDir(din, abs + s, segLen, out cino, out ct)) return false;
            ino = cino;
            if (cl > 3 && canon[cl - 1] != (byte)'\\') { if (cl >= 299) return false; canon[cl++] = (byte)'\\'; }
            for (int k = 0; k < segLen; k++) { if (cl >= 299) return false; canon[cl++] = abs[s + k]; }
        }
        canon[cl] = 0;
        canonLen = (int)cl;
        return true;
    }

    private static unsafe bool FsResolveIno(char* path, int len, out uint ino)
    {
        byte* canon = stackalloc byte[300];
        int clen = 0;
        return FsResolveCanon(path, len, out ino, canon, ref clen);
    }

    // ------------------------------------------------------------- DIR
    private static void PrintPerms(ushort mode)
    {
        PutChar(FsIsDir(mode) ? 'd' : '-');
        for (int g = 2; g >= 0; g--)
        {
            int bits = (mode >> (g * 3)) & 7;
            PutChar((bits & 4) != 0 ? 'r' : '-');
            PutChar((bits & 2) != 0 ? 'w' : '-');
            PutChar((bits & 1) != 0 ? 'x' : '-');
        }
    }

    private static void FmtUnix(ulong ts, out int day, out int mon, out int year, out int hh, out int mm)
    {
        ulong days = ts / 86400UL;
        ulong rem = ts % 86400UL;
        hh = (int)(rem / 3600UL);
        mm = (int)((rem % 3600UL) / 60UL);
        ulong z = days + 719468UL;
        ulong era = z / 146097UL;
        ulong doe = z - era * 146097UL;
        ulong yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        long y = (long)yoe + (long)era * 400;
        ulong doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        ulong mp = (5 * doy + 2) / 153;
        day = (int)(doy - (153 * mp + 2) / 5 + 1);
        mon = (int)(mp < 10 ? mp + 3 : mp - 9);
        year = (int)(y + (mon <= 2 ? 1 : 0));
    }

    private static void PrintDate(ulong ts)
    {
        int d, mo, y, h, mi;
        FmtUnix(ts, out d, out mo, out y, out h, out mi);
        WriteTwo(mo); PutChar('/'); WriteTwo(d); PutChar('/'); WriteDec((ulong)y);
        PutChar(' '); WriteTwo(h); PutChar(':'); WriteTwo(mi);
    }

    private static unsafe void DirPrint(byte* din, bool wide)
    {
        ushort dmode = InoGetMode(din);
        uint dsize = InoGetSize(din);
        ulong blocks = ((ulong)dsize + (ulong)Ext4BlockSize - 1) / (ulong)Ext4BlockSize;
        if (blocks == 0) blocks = 1;
        byte* blk = stackalloc byte[Ext4BlockSize];
        bool any = false;
        for (ulong b = 0; b < blocks; b++)
        {
            uint bnum = FsInodeGetBlock(din, b);
            if (bnum == 0) continue;
            FsReadBlock(bnum, blk);
            int off = 0;
            while (off + 8 <= Ext4BlockSize)
            {
                byte* de = blk + off;
                uint iino = DirGetIno(de);
                ushort rlen = DirGetRecLen(de);
                if (rlen == 0) break;
                byte nlen = DirGetNameLen(de);
                byte dtype = DirGetType(de);
                if (iino != 0 && nlen > 0)
                {
                    any = true;
                    byte* cin = stackalloc byte[256];
                    if (FsReadInode(iino, cin))
                    {
                        ushort cmode = InoGetMode(cin);
                        if (wide)
                        {
                            for (int k = 0; k < nlen; k++) PutChar((char)de[DirName + k]);
                            PutChar('\t');
                            if (!FsIsDir(cmode)) { PutChar(' '); }
                        }
                        else
                        {
                            PrintPerms(cmode);
                            WriteDecPad((ulong)InoGetLinks(cin), 4);
                            WriteDecPad((ulong)InoGetUid(cin), 5);
                            WriteDecPad((ulong)InoGetGid(cin), 5);
                            WriteDecPad((ulong)InoGetSize(cin), 8);
                            PutChar(' ');
                            PrintDate(Rd32(cin, InoMtime));
                            PutChar(' ');
                            for (int k = 0; k < nlen; k++) PutChar((char)de[DirName + k]);
                            PutChar('\n');
                        }
                    }
                    else
                    {
                        for (int k = 0; k < nlen; k++) PutChar((char)de[DirName + k]);
                        if (wide) PutChar('\t');
                        else PutChar('\n');
                    }
                }
                off += rlen;
                if (off > Ext4BlockSize - 8) break;
            }
        }
        if (wide && any) PutChar('\n');
    }

    private static unsafe void DirCmd(char* line, int len)
    {
        bool wide = false;
        int pathS = -1, pathE = -1;
        int i = 3;
        while (i < len)
        {
            while (i < len && line[i] == ' ') i++;
            if (i >= len) break;
            int s = i;
            while (i < len && line[i] != ' ') i++;
            int e = i;
            if (e > s)
            {
                if ((line[s] == '/' || line[s] == '-') && e == s + 2 &&
                    (line[s + 1] == 'W' || line[s + 1] == 'w')) { wide = true; continue; }
                if (line[s] == '/' || line[s] == '-') continue; // ignore other switches
                if (pathS < 0) { pathS = s; pathE = e; }
            }
        }
        uint ino;
        if (pathS < 0)
        {
            // No path: use the current directory. Build a char* copy of the
            // cwd bytes (the cwd is stored as bytes; the resolver reads chars).
            CwdInit();
            char* cb = stackalloc char[300];
            byte* outbuf = stackalloc byte[300];
            int cl = 0;
            for (int k = 0; k < _cwdALen && k < 300; k++) cb[cl++] = (char)_cwd.path[k];
            if (!FsResolveCanon(cb, cl, out ino, outbuf, ref cl))
            {
                WriteLine("Path not found");
                return;
            }
        }
        else if (!FsResolveIno(line + pathS, pathE - pathS, out ino))
        {
            WriteLine("Path not found");
            return;
        }
        byte* din = stackalloc byte[256];
        if (!FsReadInode(ino, din)) { WriteLine("Error reading inode"); return; }
        if (!FsIsDir(InoGetMode(din))) { WriteLine("Not a directory"); return; }
        DirPrint(din, wide);
    }

    // ------------------------------------------------------------- CD
    private static unsafe void CdCmd(char* line, int len)
    {
        int i = 2;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { PrintCwd(); PutChar('\n'); return; }
        uint ino;
        int clen = 0;
        bool ok;
        fixed (byte* cp = _cwd.path)
        {
            ok = FsResolveCanon(line + i, len - i, out ino, cp, ref clen);
        }
        if (!ok)
        {
            WriteLine("Invalid directory");
            return;
        }
        byte* din = stackalloc byte[256];
        if (!FsReadInode(ino, din)) { WriteLine("Invalid directory"); return; }
        if (!FsIsDir(InoGetMode(din))) { WriteLine("Invalid directory"); return; }
        _cwdALen = clen;
        WriteLine("OK");
    }

    // ------------------------------------------------------------- TYPE
    private static unsafe void TypeCmd(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { WriteLine("TYPE [file]"); return; }
        uint ino;
        if (!FsResolveIno(line + i, len - i, out ino)) { WriteLine("File not found"); return; }
        byte* cin = stackalloc byte[256];
        if (!FsReadInode(ino, cin)) { WriteLine("Error"); return; }
        if (!FsIsReg(InoGetMode(cin))) { WriteLine("Not a file"); return; }
        uint sz = InoGetSize(cin);
        byte* blk = stackalloc byte[Ext4BlockSize];
        ulong off = 0;
        while (off < sz)
        {
            ulong lblk = off / (ulong)Ext4BlockSize;
            int boff = (int)(off % (ulong)Ext4BlockSize);
            uint bnum = FsInodeGetBlock(cin, lblk);
            if (bnum == 0) break;
            FsReadBlock(bnum, blk);
            int take = Ext4BlockSize - boff;
            if ((ulong)(off + (ulong)take) > sz) take = (int)(sz - off);
            for (int k = 0; k < take; k++)
            {
                byte c = blk[boff + k];
                if (c == 0) break;
                PutChar((char)c);
            }
            off += (ulong)take;
        }
        PutChar('\n');
    }

    // ---------------------------------------------------------------- serial
    private static void SerialInit()
    {
        outb(0x3F8 + 1, 0x00);
        outb(0x3F8 + 3, 0x80);
        outb(0x3F8 + 0, 0x01);   // divisor low  (115200 / 1 = 115200? -> 38400 via DLM)
        outb(0x3F8 + 1, 0x00);
        outb(0x3F8 + 3, 0x03);   // 8N1
        outb(0x3F8 + 2, 0xC7);   // FIFO: enable, clear, 14-byte trigger
        outb(0x3F8 + 4, 0x0B);   // DTR|RTS|OUT2
        outb(0x3F8 + 1, 0x01);   // IER: enable "received data available" (IRQ4)
    }

    private static void SerialPut(char c)
    {
        if (c == '\n')
        {
            while ((inb(0x3F8 + 5) & 0x20) == 0) { }
            outb(0x3F8, (byte)'\r');
        }
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
    // system-call gate. Masks: master unmask IRQ0 (PIT), IRQ1 (kbd), IRQ2
    // (cascade, required for any slave IRQ) and IRQ4 (COM1 serial console);
    // slave unmask IRQ8 (RTC).
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
        outb(0x21, 0xE8); outb(0x80, 0);  // OCW1: unmask IRQ0 (PIT), IRQ1 (kbd), IRQ2 cascade, IRQ4 (COM1)
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

    // Serial console ring (ASCII bytes) filled by the IRQ4 handler and drained by
    // the shell, so a headless session (-serial stdio) can drive the shell too.
    private static void SerPush(byte c)
    {
        int next = _ser.tail + 1;
        if (next >= 256) next = 0;
        if (next != _ser.head)
        {
            _ser.data[_ser.tail] = c;
            _ser.tail = next;
        }
    }

    private static int SerPop()
    {
        if (_ser.head == _ser.tail) return -1;
        byte c = _ser.data[_ser.head];
        int next = _ser.head + 1;
        if (next >= 256) next = 0;
        _ser.head = next;
        return c;
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
            if (sc == 0x1D) _kbdCtrl = true;             // Ctrl pressed
            else if (sc == 0x9D) _kbdCtrl = false;       // Ctrl released
            else if (sc == 0x2E && _kbdCtrl) _ctrlC = true;  // Ctrl+C (consume it)
            else KbdPush(sc);
            PicEoi(1);
            return;
        }

        if (vector == SerVector) // IRQ4: COM1 received byte(s) (serial console)
        {
            // Drain the UART receive FIFO: LSR bit0 = data ready.
            while ((inb(0x3F8 + 5) & 0x01) != 0)
            {
                byte c = inb(0x3F8);
                if (c == 0x03) _ctrlC = true;            // Ctrl+C (consume it)
                else SerPush(c);
            }
            PicEoi(4);
            return;
        }

        if (vector == TimerVector) // IRQ0: PIT heartbeat (100 Hz)
        {
            _ticks++;
            PicEoi(0);            // EOI before switching: the PIC may deliver
            PickNext();           // the next tick while another task runs
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

        if (func == 0x30) // getpid: current process id in EAX
        {
            frame[14] = (ulong)_tasks.pid[_currentTask];
            return;
        }

        if (func == 0x4C) // exit(code in AL): become a zombie; wait() reaps us
        {
            int slot = _currentTask;
            _tasks.exitCode[slot] = (int)(rax & 0xFF);
            _tasks.state[slot] = TaskZombie;
            _tasks.fg[slot] = 0;
            if (_fgSlot == slot) _fgSlot = -1;
            PickNext();          // switch away for good (never returns)
            for (;;) halt();
        }

        if (func == 0x80) // spawn(program id in AL): returns the child's pid, or -1
        {
            int prog = (int)(rax & 0xFF);
            frame[14] = unchecked((ulong)KernelSpawn(prog, _currentTask));
            return;
        }

        if (func == 0x81) // wait(pid in RSI): blocks until the child exits
        {
            frame[14] = unchecked((ulong)WaitFor((int)rsi));
            return;
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
        _tasks.exitCode[_currentTask] = -1;   // fault code
        _tasks.state[_currentTask] = TaskZombie;
        _tasks.fg[_currentTask] = 0;
        if (_fgSlot == _currentTask) _fgSlot = -1;
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

        // --- TSS body: no I/O bitmap; RSP0 is set when a ring-3 task runs ---
        for (int i = 0; i < 104; i++) _tss.bytes[i] = 0;
        fixed (byte* tp = _tss.bytes)
        {
            tp[100] = 0x68; tp[101] = 0x00;                   // IOPB = 104 (none)
        }
        SetTssRsp0(1);                                        // sane default (slot 1)

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

    // --------------------------------------------------- physical memory
    // Walk the Multiboot2 memory map to find the biggest usable RAM region
    // above the kernel (falls back to 64 MiB, the QEMU default).
    private static void ParseMmap(ulong info)
    {
        ulong floor = PhysFloorDefault;
        ulong top = PhysTopDefault;
        ulong bestLen = 0;

        if (info != 0)
        {
            uint total = *(uint*)info;
            ulong p = info + 8;
            ulong end = info + total;
            while (p + 8 <= end)
            {
                uint type = *(uint*)p;
                uint size = *(uint*)(p + 4);
                if (type == 0) break;              // end tag
                if (size < 8) break;
                if (type == 6 && size >= 16)       // memory map tag
                {
                    uint entrySize = *(uint*)(p + 8);
                    ulong e = p + 16;
                    ulong eend = p + size;
                    while (entrySize != 0 && e + entrySize <= eend)
                    {
                        ulong baddr = *(ulong*)e;
                        ulong len = *(ulong*)(e + 8);
                        uint mtype = *(uint*)(e + 16);
                        if (mtype == 1 && baddr >= 0x100000 && len > bestLen)
                        {
                            bestLen = len;
                            floor = baddr < PhysFloorDefault ? PhysFloorDefault : baddr;
                            top = baddr + len;
                        }
                        e += entrySize;
                    }
                }
                p += size;
            }
        }
        if (top > 0x40000000UL) top = 0x40000000UL;    // identity map is 1 GiB
        _physFloor = (floor + 0xFFF) & ~0xFFFUL;
        _physTop = top & ~0xFFFUL;
        if (_physTop <= _physFloor) { _physFloor = PhysFloorDefault; _physTop = PhysTopDefault; }
    }

    private static void FramesInit()
    {
        _frameTotal = (_physTop - _physFloor) / FrameSize;
        if (_frameTotal > (ulong)MaxFrames) _frameTotal = (ulong)MaxFrames;
        for (int i = 0; i < MaxFrames / 8; i++) _frames.bits[i] = 0;
        _frameUsed = 0;
        // Never hand out the pd[2] window: inside VA 0x400000..0x5FFFFF the
        // virtual address is remapped, so the kernel could not reach a fresh
        // block through its physical address there.
        ReserveRange(ReservedLo, ReservedHi);
    }

    private static void ReserveRange(ulong lo, ulong hi)
    {
        if (lo < _physFloor) lo = _physFloor;
        if (hi > _physTop) hi = _physTop;
        ulong i = (lo - _physFloor) / FrameSize;
        ulong j = (hi - _physFloor) / FrameSize;
        for (; i < j; i++) FrameSet(i);
    }

    private static bool FrameGet(ulong i)
    {
        return (_frames.bits[i >> 3] & (byte)(1 << (int)(i & 7))) != 0;
    }

    private static void FrameSet(ulong i)
    {
        _frames.bits[i >> 3] |= (byte)(1 << (int)(i & 7));
        _frameUsed++;
    }

    private static void FrameClear(ulong i)
    {
        _frames.bits[i >> 3] &= (byte)~(1 << (int)(i & 7));
    }

    // Find n consecutive free frames at the given alignment (bytes).
    private static ulong AllocFrames(int n, ulong align)
    {
        ulong stride = align / FrameSize;
        if (stride < 1) stride = 1;
        for (ulong i = 0; i + (ulong)n <= _frameTotal; i += stride)
        {
            bool ok = true;
            for (int j = 0; j < n; j++)
                if (FrameGet(i + (ulong)j)) { ok = false; break; }
            if (!ok) continue;
            for (int j = 0; j < n; j++) FrameSet(i + (ulong)j);
            return _physFloor + i * FrameSize;
        }
        return 0;
    }

    private static void FreeFrames(ulong phys, int n)
    {
        if (phys < _physFloor || phys >= _physTop) return;
        ulong i = (phys - _physFloor) / FrameSize;
        for (int j = 0; j < n; j++)
        {
            ulong k = i + (ulong)j;
            if (k >= _frameTotal) break;
            if (FrameGet(k)) { FrameClear(k); if (_frameUsed > 0) _frameUsed--; }
        }
    }

    // Results of the last CreateAddressSpace call (avoids out-parameters).
    private static ulong _asBlock, _asCr3, _asPt;

    // Build a fresh address space for a process: copy the kernel's identity
    // mappings, then point pd[2] at a private 2 MiB block holding the program
    // image at VA 0x400000 (stack near VA 0x480000). Returns false if no memory.
    private static bool CreateAddressSpace(ulong blobSrc, ulong blobEnd)
    {
        ulong block = AllocFrames(BlockFrames, BlockAlign);
        if (block == 0) return false;
        ulong pt = AllocFrames(3, FrameSize);
        if (pt == 0) { FreeFrames(block, BlockFrames); return false; }

        byte* b = (byte*)block;
        for (ulong i = 0; i < (ulong)BlockFrames * FrameSize; i++) b[i] = 0;
        byte* s = (byte*)blobSrc;
        for (ulong i = 0; i < blobEnd - blobSrc; i++) b[i] = s[i];

        ulong pml4 = pt, pdpt = pt + 4096, pd = pt + 8192;
        ulong* P = (ulong*)pml4;
        ulong* D = (ulong*)pdpt;
        ulong* T = (ulong*)pd;
        for (int i = 0; i < 512; i++) { P[i] = 0; D[i] = 0; }
        ulong* M = (ulong*)osb_master_pd();
        for (int i = 0; i < 512; i++) T[i] = M[i];
        T[2] = block | 0x87;                 // present, rw, user, PS (2 MiB)
        P[0] = pdpt | 0x7;                   // present, rw, user
        D[0] = pd | 0x7;

        _asBlock = block; _asCr3 = pml4; _asPt = pt;
        return true;
    }

    private static void DestroyAddressSpace(int slot)
    {
        if (_tasks.ublock[slot] != 0) { FreeFrames(_tasks.ublock[slot], BlockFrames); _tasks.ublock[slot] = 0; }
        if (_tasks.ptphys[slot] != 0) { FreeFrames(_tasks.ptphys[slot], 3); _tasks.ptphys[slot] = 0; }
        _tasks.cr3[slot] = osb_master_pml4();
    }

    // ----------------------------------------------- task table / job control
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
        for (int i = 0; i < TaskCount; i++)
        {
            _tasks.state[i] = TaskFree;
            _tasks.kind[i] = KindNone;
            _tasks.fg[i] = 0;
            _tasks.pid[i] = 0;
            _tasks.ppid[i] = 0;
            _tasks.exitCode[i] = 0;
            _tasks.sp[i] = 0;
            _tasks.cr3[i] = osb_master_pml4();
            _tasks.ublock[i] = 0;
            _tasks.ptphys[i] = 0;
        }
        // Slot 0 is the shell/console, running on the boot stack in the kernel
        // address space. Its saved SP is captured by the first osb_switch out.
        _tasks.state[SlotConsole] = TaskReady;
        _tasks.kind[SlotConsole] = KindShell;
        _tasks.pid[SlotConsole] = 1;
        SetName(SlotConsole, "CONSOLE");
        _currentTask = SlotConsole;
        _nextPid = 2;
        _fgSlot = -1;
    }

    private static void SetName(int slot, string name)
    {
        int o = slot * MaxName;
        for (int i = 0; i < MaxName; i++) _tasks.name[o + i] = 0;
        int n = name.Length; if (n > MaxName - 1) n = MaxName - 1;
        fixed (char* p = name)
        {
            for (int i = 0; i < n; i++) _tasks.name[o + i] = (byte)p[i];
        }
    }

    private static int AllocSlot()
    {
        for (int i = SlotConsole + 1; i < TaskCount; i++)
            if (_tasks.state[i] == TaskFree) return i;   // skip zombies (not yet reaped)
        return -1;
    }

    // Point the TSS RSP0 at a slot's kernel stack top: the CPU lands there on the
    // first ring-0 transition from that ring-3 task (and every return to ring 3
    // re-uses it until another ring-3 task is scheduled).
    private static void SetTssRsp0(int slot)
    {
        ulong ktop;
        fixed (byte* p = _stacks.bytes)
            ktop = ((ulong)p + (ulong)slot * StackSize + StackSize) & ~(ulong)15;
        fixed (byte* tp = _tss.bytes)
            *(ulong*)(tp + 4) = ktop;
    }

    private static void SeedSlot(int slot, ulong entry, string name, int kind)
    {
        ulong sp;
        fixed (byte* p = _stacks.bytes)
            sp = SeedTask(p + slot * StackSize, StackSize, entry);
        _tasks.sp[slot] = sp;
        _tasks.pid[slot] = _nextPid++;
        _tasks.ppid[slot] = _tasks.pid[_currentTask];
        _tasks.state[slot] = TaskReady;
        _tasks.kind[slot] = kind;
        _tasks.fg[slot] = 0;
        _tasks.exitCode[slot] = 0;
        _tasks.arg[slot] = 0;
        _tasks.cr3[slot] = osb_master_pml4();
        _tasks.ublock[slot] = 0;
        _tasks.ptphys[slot] = 0;
        _tasks.round[slot] = 0;
        _tasks.deadline[slot] = 0;
        SetName(slot, name);
    }

    // Force-terminate a slot: release its address space and free the slot. Used
    // by KILL and by Ctrl+C on a foreground job.
    private static void KillSlot(int slot)
    {
        if (slot <= SlotConsole) return;
        DestroyAddressSpace(slot);
        _tasks.state[slot] = TaskFree;
        _tasks.kind[slot] = KindNone;
        _tasks.fg[slot] = 0;
        SetName(slot, "free");
        if (_fgSlot == slot) _fgSlot = -1;
    }

    // Reap a zombie: collect its exit code, then release everything.
    private static int ReapSlot(int slot)
    {
        int code = _tasks.exitCode[slot];
        KillSlot(slot);
        return code;
    }

    // Find a live (ready) or zombie slot by pid; -1 if unknown.
    private static int FindPid(int pid)
    {
        for (int i = 0; i < TaskCount; i++)
            if (_tasks.pid[i] == pid && (i == SlotConsole || _tasks.state[i] != TaskFree))
                return i;
        return -1;
    }

    // Called from the IRQ0 handler on every tick. Round-robins to the next ready
    // task by swapping kernel stacks; the switch is atomic (interrupt gate) and
    // the interrupted ISR frame stays parked on the stopped task's stack.
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

        if (_tasks.kind[next] == KindUser) SetTssRsp0(next);
        osb_load_cr3(_tasks.cr3[next]);               // per-process address space
        _currentTask = next;
        fixed (ulong* sp = _tasks.sp)
            osb_switch(sp + cur, sp + next);
    }

    // Job body: demo. Runs on its slot's kernel stack, prints once per second and
    // sleeps in between. It never yields: the PIT preempts it. Persists until
    // killed or terminated.
    [RuntimeExport("osb_demo_task_main")]
    private static void DemoTaskMain()
    {
        int slot = _currentTask;
        ulong pid = (ulong)_tasks.pid[slot];
        for (;;)
        {
            if (_ticks >= _tasks.deadline[slot])
            {
                _tasks.round[slot]++;
                Write("DEMO [");
                WriteDec(pid);
                Write("]: round ");
                WriteDec(_tasks.round[slot]);
                Write("  (tick ");
                WriteDec(_ticks);
                WriteLine(")");
                _tasks.deadline[slot] = _ticks + (ulong)PitHz;   // next round in 1 s
            }
            halt(); // sleep until the next interrupt; the PIT schedules the rest
        }
    }

    // Job body: ring-3 program. The scheduler has already loaded this process's
    // CR3, so VA 0x400000 is its private block. Drops to CPL3 and never comes
    // back (it exits via INT 21h or faults).
    [RuntimeExport("osb_user_task_main")]
    private static void UserTaskMain()
    {
        int slot = _currentTask;
        osb_enter_user(_tasks.arg[slot], UserStackTop);
        for (;;) halt();                              // not reached
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


    // ---------------------------------------------------------------- env
    // Sets/changes a variable. Names are stored uppercase so lookups are
    // case-insensitive (DOS/OSB convention). Values keep their characters.
    // Redefining a variable appends its new value to the value buffer (the old
    // bytes are left behind); fine for the shell-size 4 KiB buffer.
    private static unsafe bool EnvSetChars(char* name, int nlen, char* value, int vlen)
    {
        if (nlen <= 0) return false;
        for (int i = 0; i < _envCount; i++)
        {
            if (_env.nameLen[i] != nlen) continue;
            bool same = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { same = false; break; }
            }
            if (!same) continue;
            if (_envValuePos + vlen > EnvValueBufSize) return false;
            _env.valueOff[i] = _envValuePos;
            _env.valueLen[i] = vlen;
            for (int k = 0; k < vlen; k++) _env.values[_envValuePos + k] = (byte)value[k];
            _envValuePos += vlen;
            return true;
        }
        if (_envCount >= MaxEnvVars) return false;
        if (_envNamePos + nlen > EnvNameBufSize || _envValuePos + vlen > EnvValueBufSize) return false;
        int idx = _envCount;
        _envCount++;
        _env.nameOff[idx] = _envNamePos;
        _env.nameLen[idx] = nlen;
        for (int k = 0; k < nlen; k++)
        {
            char c = name[k];
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            _env.names[_envNamePos + k] = (byte)c;
        }
        _envNamePos += nlen;
        _env.valueOff[idx] = _envValuePos;
        _env.valueLen[idx] = vlen;
        for (int k = 0; k < vlen; k++) _env.values[_envValuePos + k] = (byte)value[k];
        _envValuePos += vlen;
        return true;
    }

    private static unsafe bool EnvSet(string name, string value)
    {
        fixed (char* n = name)
        fixed (char* v = value)
        {
            return EnvSetChars(n, name.Length, v, value.Length);
        }
    }

    // Case-insensitive lookup; returns the value's range in the value buffer.
    private static unsafe bool EnvFind(char* name, int nlen, out int vo, out int vl)
    {
        vo = 0;
        vl = 0;
        for (int i = 0; i < _envCount; i++)
        {
            if (_env.nameLen[i] != nlen) continue;
            bool same = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { same = false; break; }
            }
            if (same)
            {
                vo = _env.valueOff[i];
                vl = _env.valueLen[i];
                return true;
            }
        }
        return false;
    }

    private static void EnvListAll()
    {
        for (int i = 0; i < _envCount; i++)
        {
            for (int k = 0; k < _env.nameLen[i]; k++)
                PutChar((char)_env.names[_env.nameOff[i] + k]);
            PutChar('=');
            for (int k = 0; k < _env.valueLen[i]; k++)
                PutChar((char)_env.values[_env.valueOff[i] + k]);
            PutChar('\n');
        }
    }

    private static void EnvListPrefix(char* name, int nlen)
    {
        bool any = false;
        for (int i = 0; i < _envCount; i++)
        {
            if (_env.nameLen[i] < nlen) continue;
            bool pref = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { pref = false; break; }
            }
            if (!pref) continue;
            any = true;
            for (int k = 0; k < _env.nameLen[i]; k++)
                PutChar((char)_env.names[_env.nameOff[i] + k]);
            PutChar('=');
            for (int k = 0; k < _env.valueLen[i]; k++)
                PutChar((char)_env.values[_env.valueOff[i] + k]);
            PutChar('\n');
        }
        if (!any) WriteLine("Environment variable not defined.");
    }

    // SET [NAME]        list variables starting with NAME (or all)
    // SET NAME=value    define/redefine an environment variable
    private static unsafe void CmdSet(char* line, int len)
    {
        int i = 3;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { EnvListAll(); return; }
        int eq = i;
        while (eq < len && line[eq] != '=') eq++;
        if (eq >= len)
        {
            EnvListPrefix(line + i, len - i);
            return;
        }
        int nl = eq - i;
        int vs = eq + 1;
        int vl = len - vs;
        char* nameb = stackalloc char[64];
        char* valb = stackalloc char[256];
        for (int k = 0; k < nl && k < 63; k++) nameb[k] = line[i + k];
        for (int k = 0; k < vl && k < 255; k++) valb[k] = line[vs + k];
        EnvSetChars(nameb, nl, valb, vl);
    }

    // Expands $VAR (names are case-insensitive) into dst. Undefined variables
    // round-trip literally, so SET PROMPT=$P$G keeps its special characters.
    private static unsafe int EnvExpand(char* src, int slen, char* dst, int dcap)
    {
        int o = 0;
        int i = 0;
        while (i < slen && o < dcap - 1)
        {
            if (src[i] != '$') { dst[o++] = src[i++]; continue; }
            if (i + 1 >= slen) { dst[o++] = '$'; i++; continue; }
            char n0 = src[i + 1];
            bool ident = (n0 >= 'A' && n0 <= 'Z') || (n0 >= 'a' && n0 <= 'z') || n0 == '_';
            if (!ident)
            {
                dst[o++] = '$';
                i++;
                continue;
            }
            int st = i + 1;
            int j = st;
            while (j < slen && (((src[j] >= 'A' && src[j] <= 'Z') || (src[j] >= 'a' && src[j] <= 'z') ||
                                 (src[j] >= '0' && src[j] <= '9') || src[j] == '_'))) j++;
            int nameLen = j - st;
            char* nm = stackalloc char[64];
            for (int k = 0; k < nameLen && k < 63; k++) nm[k] = src[st + k];
            int vo;
            int vl;
            if (EnvFind(nm, nameLen, out vo, out vl))
            {
                for (int k = 0; k < vl && o < dcap - 1; k++) dst[o++] = (char)_env.values[vo + k];
                i = j;
                continue;
            }
            dst[o++] = '$';
            i++;
        }
        dst[o] = '\0';
        return o;
    }

    // ---- DOS-style prompt ----
    // $P path  $N drive  $G >  $L <  $B |  $Q =  $S space  $D date  $T time
    // $V version  $H backspace  $E ESC  $_ newline  $$ literal $, or any
    // variable such as $USER/$HOME.
    private static unsafe void WritePrompt()
    {
        char* prompt = stackalloc char[128];
        int plen;
        fixed (char* pr = "PROMPT")
        {
            int po, pl;
            if (EnvFind(pr, 6, out po, out pl))
            {
                plen = pl < 127 ? pl : 127;
                for (int i = 0; i < plen; i++) prompt[i] = (char)_env.values[po + i];
            }
            else
            {
                prompt[0] = '$'; prompt[1] = 'P'; prompt[2] = '$'; prompt[3] = 'G';
                plen = 4;
            }
        }
        prompt[plen] = '\0';
        for (int k = 0; k < plen; k++)
        {
            if (prompt[k] != '$') { PutChar(prompt[k]); continue; }
            if (k + 1 >= plen) { PutChar('$'); continue; }
            char t = prompt[k + 1];
            char ut = Up(t);
            bool known = ut == 'N' || ut == 'P' || ut == 'G' || ut == 'L' || ut == 'B' ||
                         ut == 'Q' || ut == 'S' || ut == 'D' || ut == 'T' || ut == 'V' ||
                         ut == 'H' || ut == 'E' || ut == '$' || ut == '_';
            if (known)
            {
                k++;
                switch (ut)
                {
                    case 'N': PutChar(_curDrive); break;
                    case 'G': PutChar('>'); break;
                    case 'L': PutChar('<'); break;
                    case 'B': PutChar('|'); break;
                    case 'Q': PutChar('='); break;
                    case 'S': PutChar(' '); break;
                    case 'D':
                        WriteTwo(_rtcDay); PutChar('/'); WriteTwo(_rtcMonth); PutChar('/');
                        WriteDec(2000UL + (ulong)_rtcYear);
                        break;
                    case 'T':
                        WriteTwo(_rtcHour); PutChar(':');
                        WriteTwo(_rtcMin); PutChar(':');
                        WriteTwo(_rtcSec);
                        break;
                    case 'V': Write("OSB 0.1"); break;
                    case '$': PutChar('$'); break;
                    case 'H': PutChar('\b'); break;
                    case 'E': PutChar((char)27); break;
                    case '_': PutChar('\n'); break;
                    case 'P': PrintCwd(); break;
                }
                continue;
            }
            int st = k + 1;
            int j = st;
            while (j < plen && (((prompt[j] >= 'A' && prompt[j] <= 'Z') || (prompt[j] >= 'a' && prompt[j] <= 'z') ||
                                (prompt[j] >= '0' && prompt[j] <= '9') || prompt[j] == '_'))) j++;
            if (j > st)
            {
                int nameLen = j - st;
                char* nm = stackalloc char[64];
                for (int q = 0; q < nameLen && q < 63; q++) nm[q] = prompt[st + q];
                int vo, vl;
                if (EnvFind(nm, nameLen, out vo, out vl))
                {
                    for (int q = 0; q < vl; q++) PutChar((char)_env.values[vo + q]);
                    k = j - 1;
                    continue;
                }
                PutChar('$');
                for (int q = st; q < j; q++) PutChar(prompt[q]);
                k = j - 1;
                continue;
            }
            PutChar('$');
        }
    }

    private static void CwdInit()
    {
        if (_cwdALen == 0)
        {
            _cwd.path[0] = (byte)'A'; _cwd.path[1] = (byte)':'; _cwd.path[2] = (byte)'\\';
            _cwdALen = 3;
        }
    }

    private static void PrintCwd()
    {
        CwdInit();
        for (int i = 0; i < _cwdALen; i++) PutChar((char)_cwd.path[i]);
    }

    // Boot defaults for the OSB session environment.
    private static unsafe void EnvInit()
    {
        EnvSet("PATH", "A:\\OSB\\BIN;A:\\OSB\\CONF");
        EnvSet("PROMPT", "$P$G");
        EnvSet("USER", "guest");
        EnvSet("HOME", "A:\\HOME\\guest");
        EnvSet("TEMP", "A:\\OSB\\TEMP");
        EnvSet("COMSPEC", "A:\\OSB\\BIN\\COMMAND.COM");
        EnvSet("OS", "OSB");
        EnvSet("VER", "0.1.0");
        EnvSet("LASTDRIVE", "Z");
    }

    // ---------------------------------------------------------------- shell
    private static void ShellLoop()
    {
        WritePrompt(); PutChar('\n');
        char* line = stackalloc char[128];
        int len = 0;
        bool shift = false;

        while (true)
        {
            if (_ctrlC) { _ctrlC = false; len = 0; PutChar('\n'); WritePrompt(); continue; }

            // Serial console first: this is how a headless session
            // (-display none -serial stdio) drives the shell.
            int ch = SerPop();
            if (ch >= 0) { FeedChar((char)ch, line, ref len); continue; }

            // Then the IRQ1 scancode ring. When both are empty, sleep: the PIT
            // handler preempts us and schedules the other tasks.
            int sc = KbdPop();
            if (sc < 0) { halt(); continue; }

            if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
            if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
            if ((sc & 0x80) != 0) continue; // ignore other break codes

            if (sc == 0x1C) { FeedChar('\r', line, ref len); continue; } // Enter
            if (sc == 0x0E) { FeedChar('\b', line, ref len); continue; } // Backspace

            char c = MapScancode((byte)sc, shift);
            if (c != 0) FeedChar(c, line, ref len);
        }
    }

    // Apply one logical character (from PS/2 or the serial console) to the input
    // line: echo printable characters, handle Backspace, and run the line on
    // Enter. Keeps both input paths identical.
    private static void FeedChar(char c, char* line, ref int len)
    {
        if (c == '\r' || c == '\n')
        {
            PutChar('\n');
            line[len] = '\0';
            ExecuteLine(line, len);
            len = 0;
            WritePrompt();
            return;
        }
        if (c == '\b' || c == (char)0x7F)
        {
            if (len > 0)
            {
                len--;
                PutChar('\b');
                PutChar(' ');
                PutChar('\b');
            }
            return;
        }
        if (c >= ' ' && c < 127 && len < 127)
        {
            line[len++] = c;
            PutChar(c);
        }
    }

    private static void ExecuteLine(char* line, int len)
    {
        if (len == 0) return;

        // Expand $VAR references ($USER, $HOME, ...) before dispatch.
        // Undefined names round-trip, so SET PROMPT=$P$G keeps its $ tokens.
        char* expanded = stackalloc char[256];
        int elen = EnvExpand(line, len, expanded, 256);
        line = expanded;
        len = elen;
        if (len == 0) return;

        // A trailing '&' means "run this command as a background job".
        bool background = false;
        while (len > 0 && line[len - 1] == ' ') len--;
        if (len > 0 && line[len - 1] == '&')
        {
            background = true;
            len--;
            while (len > 0 && line[len - 1] == ' ') len--;
        }
        if (len == 0) return;

        if (Eq(line, len, "VER")) { WriteLine("OSB (dotnet) version 0.1 - bootable kernel"); return; }
        if (Eq(line, len, "ABOUT")) { WriteLine("OSB: Operating System Basic, created in 2000, rebooted in C#."); return; }
        if (Eq(line, len, "SET") || StartsWith(line, len, "SET "))
        {
            CmdSet(line, len);
            return;
        }
        if (Eq(line, len, "DIR") || StartsWith(line, len, "DIR "))
        {
            DirCmd(line, len);
            return;
        }
        if (Eq(line, len, "CD") || StartsWith(line, len, "CD "))
        {
            CdCmd(line, len);
            return;
        }
        if (Eq(line, len, "TYPE") || StartsWith(line, len, "TYPE "))
        {
            TypeCmd(line, len);
            return;
        }
        if (Eq(line, len, "HELP"))
        {
            WriteLine("Commands: VER  ABOUT  HELP  CLS  ECHO <text>  SET  TICKS");
            WriteLine("          SET [NAME=value]  env ($VAR, case-insensitive names)");
            WriteLine("Files:    DIR [path] [/W]  CD [path]  TYPE <file>  (ext4/A:)");
            WriteLine("          SLEEP <s>  TIME  DATE  MEM  HALT  POWEROFF  REBOOT");
            WriteLine("Processes:SPAWN [BAD|SPAWNER] [&]  start a ring-3 process");
            WriteLine("          RING3 [BAD] [&]  synonym for SPAWN");
            WriteLine("          TASK / PS        list processes (a simple ps)");
            WriteLine("          KILL <pid>       terminate a process");
            WriteLine("          WAIT <pid>       wait for a child and collect its code");
            WriteLine("          TAB <pid>        bring a process to the foreground");
            WriteLine("          DEMO [&]         start a demo (kernel) task");
            WriteLine("          Add '&' to run in the background; Ctrl+C aborts a");
            WriteLine("          foreground job and returns to the shell.");
            return;
        }
        if (Eq(line, len, "CLS")) { VgaClear(); return; }
        if (Eq(line, len, "MEM")) { PrintMem(); return; }
        if (Eq(line, len, "DEMO")) { SpawnDemo(background); return; }
        if (Eq(line, len, "TASK") || Eq(line, len, "PS")) { PrintTaskList(); return; }
        if (Eq(line, len, "KILL") || StartsWith(line, len, "KILL ")) { CmdKill(line, len); return; }
        if (Eq(line, len, "WAIT") || StartsWith(line, len, "WAIT ")) { CmdWait(line, len); return; }
        if (Eq(line, len, "TAB") || StartsWith(line, len, "TAB ")) { CmdTab(line, len); return; }
        if (Eq(line, len, "SPAWN") || StartsWith(line, len, "SPAWN ") ||
            Eq(line, len, "RING3") || StartsWith(line, len, "RING3 "))
        {
            int i = 5;
            while (i < len && line[i] == ' ') i++;
            int prog = ProgHello;
            if (StartsWithAt(line, len, i, "SPAWNER")) prog = ProgSpawner;
            else if (StartsWithAt(line, len, i, "BAD")) prog = ProgBad;
            SpawnProcess(prog, background);
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

    // Case-insensitive match of `lit` starting at `line[at]`.
    private static bool StartsWithAt(char* line, int len, int at, string lit)
    {
        if (at < 0 || at + lit.Length > len) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < lit.Length; i++)
                if (Up(line[at + i]) != p[i]) return false;
        }
        return true;
    }

    // ------------------------------------------------------------ job control
    // Start a DEMO job (kernel task; no user address space). Without '&' it
    // becomes the foreground job and the shell waits; with '&' the prompt returns.
    private static void SpawnDemo(bool background)
    {
        int slot = AllocSlot();
        if (slot < 0) { WriteLine("No free task slot."); return; }
        SeedSlot(slot, osb_sched_get_demo_entry(), "DEMO", KindDemo);
        Write("Started DEMO [");
        WriteDec((ulong)_tasks.pid[slot]);
        WriteLine(background ? "] in the background." : "] in the foreground.");
        if (!background) { _fgSlot = slot; _tasks.fg[slot] = 1; WaitForeground(slot); }
    }

    // Create a process running a built-in ring-3 program, in its own address
    // space. Returns the new pid (or -1). Does not wait.
    private static int KernelSpawn(int prog, int parentSlot)
    {
        int slot = AllocSlot();
        if (slot < 0) { WriteLine("No free task slot."); return -1; }

        ulong src, end;
        if (prog == ProgBad) { src = osb_userbad_begin(); end = osb_userbad_end(); }
        else if (prog == ProgSpawner) { src = osb_spawner_begin(); end = osb_spawner_end(); }
        else { src = osb_userstub_begin(); end = osb_userstub_end(); }

        if (!CreateAddressSpace(src, end)) { WriteLine("Out of memory."); return -1; }

        SeedSlot(slot, osb_sched_get_user_entry(), ProgName(prog), KindUser);
        _tasks.arg[slot] = UserBase;                 // entry RIP in the private block
        _tasks.cr3[slot] = _asCr3;
        _tasks.ublock[slot] = _asBlock;
        _tasks.ptphys[slot] = _asPt;
        _tasks.ppid[slot] = _tasks.pid[parentSlot];
        return _tasks.pid[slot];
    }

    private static string ProgName(int prog)
    {
        if (prog == ProgBad) return "BAD";
        if (prog == ProgSpawner) return "SPAWNER";
        return "HELLO";
    }

    private static void SpawnProcess(int prog, bool background)
    {
        int pid = KernelSpawn(prog, SlotConsole);
        if (pid < 0) return;
        int slot = FindPid(pid);
        Write("Spawned ");
        Write(ProgName(prog));
        Write(" [");
        WriteDec((ulong)pid);
        Write(background ? "] in the background" : "] in the foreground");
        Write(" (block ");
        WriteHex(_tasks.ublock[slot]);
        WriteLine(").");
        if (!background) { _fgSlot = slot; _tasks.fg[slot] = 1; WaitForeground(slot); }
    }

    // Block until the foreground job ends (zombie), is aborted (Ctrl+C), or is
    // otherwise gone. A zombie is reaped here and its exit code shown.
    private static void WaitForeground(int slot)
    {
        Write("Foreground [");
        WriteDec((ulong)_tasks.pid[slot]);
        WriteLine("]  (Ctrl+C returns to the shell)");
        bool aborted = false;
        while (_tasks.state[slot] == TaskReady)
        {
            if (_ctrlC) { _ctrlC = false; aborted = true; break; }  // set by the IRQ
            halt();
        }
        PutChar('\n');
        if (aborted)
        {
            KillSlot(slot);
            WriteLine("Foreground job aborted (Ctrl+C).");
        }
        else if (_tasks.state[slot] == TaskZombie)
        {
            int code = ReapSlot(slot);
            Write("Job finished, exit code ");
            WriteDec((ulong)(code & 0xFF));
            WriteLine(".");
        }
        else
        {
            WriteLine("Job ended.");
        }
        _fgSlot = -1;
    }

    // wait(pid): block until the process exits, then reap it. Returns the exit
    // code, or -1 if the pid is unknown / already gone.
    private static int WaitFor(int pid)
    {
        int slot = FindPid(pid);
        if (slot < 0) return -1;
        while (_tasks.state[slot] == TaskReady) halt();
        if (_tasks.state[slot] == TaskZombie) return ReapSlot(slot) & 0xFF;
        return -1;   // vanished (e.g. killed)
    }

    private static void CmdWait(char* line, int len)
    {
        int pid = ParseIntAt(line, 4, len);
        if (pid == 0) { WriteLine("Usage: WAIT <pid>"); return; }
        Write("Waiting for pid ");
        WriteDec((ulong)pid);
        WriteLine(" ...");
        int code = WaitFor(pid);
        if (code < 0) { WriteLine("No such process (or already gone)."); return; }
        Write("Process ");
        WriteDec((ulong)pid);
        Write(" exited, code ");
        WriteDec((ulong)code);
        WriteLine(".");
    }

    private static void CmdKill(char* line, int len)
    {
        int pid = ParseIntAt(line, 4, len);
        int slot = FindPid(pid);
        if (pid == 0 || slot < 0) { WriteLine("No such process."); return; }
        if (slot == SlotConsole) { WriteLine("Cannot kill the console."); return; }
        KillSlot(slot);
        Write("Killed pid ");
        WriteDec((ulong)pid);
        WriteLine(".");
    }

    private static void CmdTab(char* line, int len)
    {
        int pid = ParseIntAt(line, 3, len);
        int slot = FindPid(pid);
        if (pid == 0 || slot < 0) { WriteLine("No such process."); return; }
        if (slot == SlotConsole) { WriteLine("The console is already in the foreground."); return; }
        if (_tasks.state[slot] != TaskReady) { WriteLine("Process is not running."); return; }
        _fgSlot = slot;
        _tasks.fg[slot] = 1;
        WaitForeground(slot);
    }

    // A simple "ps": pid, name, state, memory and foreground/background.
    private static void PrintTaskList()
    {
        WriteLine(" PID  NAME        STATE    MEMORY      ROLE");
        for (int i = 0; i < TaskCount; i++)
        {
            if (i != SlotConsole && _tasks.state[i] == TaskFree) continue;
            WriteDecPad((ulong)_tasks.pid[i], 4);
            PutChar(' ');
            WriteNamePad(i, 10);
            PutChar(' ');
            Write(_tasks.state[i] == TaskZombie ? "zombie   " : "run      ");
            if (_tasks.ublock[i] != 0) WriteHex(_tasks.ublock[i]);
            else Write("kernel    ");
            PutChar(' ');
            if (i == SlotConsole) WriteLine("fg");
            else WriteLine(_tasks.state[i] == TaskZombie ? "-" : (i == _fgSlot ? "fg" : "bg"));
        }
    }

    private static void PrintMem()
    {
        Write("Physical frames: ");
        WriteDec(_frameUsed);
        Write(" used / ");
        WriteDec(_frameTotal);
        Write(" total  (");
        WriteDec((_physTop - _physFloor) / 1024);
        WriteLine(" KiB usable)");
        Write("Range: ");
        WriteHex(_physFloor);
        Write(" - ");
        WriteHex(_physTop);
        WriteLine("");
    }

    private static int ParseIntAt(char* line, int start, int len)
    {
        int i = start;
        while (i < len && line[i] == ' ') i++;
        int v = 0;
        while (i < len && line[i] >= '0' && line[i] <= '9')
        {
            v = v * 10 + (line[i] - '0');
            i++;
        }
        return v;
    }

    private static void WriteDecPad(ulong v, int width)
    {
        int digits = 1;
        ulong t = v;
        while (t >= 10) { t /= 10; digits++; }
        for (int i = digits; i < width; i++) PutChar(' ');
        WriteDec(v);
    }

    private static void WriteNamePad(int slot, int width)
    {
        int o = slot * MaxName;
        int n = 0;
        while (n < MaxName && _tasks.name[o + n] != 0) n++;
        for (int i = 0; i < n; i++) PutChar((char)_tasks.name[o + i]);
        for (int i = n; i < width; i++) PutChar(' ');
    }
}
