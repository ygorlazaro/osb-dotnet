using System;

namespace Osb.Kernel.Memory;

internal static class Layout
{
    // VGA
    public const uint VgaBase = 0xB8000;
    public const int VgaWidth = 80;
    public const int VgaHeight = 25;
    public const byte Attr = 0x07; // light grey on black

    // IDT + PIC
    public const int IdtSize = 256 * 16;          // 256 entries x 16 bytes
    public const ushort KernelSelector = 0x08;    // GDT: 64-bit code at offset 8
    public const byte IdtInterruptGate = 0x8E;    // present, DPL0, 64-bit gate
    public const byte IdtUserGate = 0xEE;         // present, DPL3 (callable via INT)
    // The PIC is remapped to vectors 0x30..0x3F so that vector 0x21 is free for
    // the DOS-style system-call entry point (INT 21h).
    public const int PicBase = 0x30;
    public const int KbdVector = PicBase + 1;     // IRQ1 -> vector 0x31
    public const int TimerVector = PicBase + 0;   // IRQ0 -> vector 0x30
    public const int SerVector = PicBase + 4;     // IRQ4 -> vector 0x34 (COM1 RX)
    public const int RtcVector = PicBase + 8;     // IRQ8 -> vector 0x38
    public const int Int21Vector = 0x21;          // DOS-style syscall gate
    public const int PitHz = 100;                 // PIT frequency in Hz

    // Ring 3 / user memory layout
    // Every process has the same user virtual layout (code at 0x400000, stack
    // just below 0x480000) but a *private* 2 MiB physical block behind it, so
    // one process cannot see another's memory. The block is mapped at pd[2].
    public const ulong UserBase = 0x400000;       // user code/data region start
    public const ulong UserLimit = 0x600000;      // end of the U/S=1 2 MiB page
    public const ulong UserStackTop = 0x480000;   // user stack grows down from here

    // Physical memory (frame allocator)
    public const ulong PhysFloorDefault = 0x200000;   // just above the kernel image
    public const ulong PhysTopDefault = 0x4000000;    // 64 MiB (QEMU -m 64M)
    // Ceiling of the boot-time identity map: boot.S builds one page directory
    // per GiB under pdpt[0..63], so the kernel addresses the first 64 GiB.
    public const ulong PhysMapLimit = 0x1000000000UL; // 64 GiB
    public const int FrameSize = 4096;
    public const int BlockFrames = 512;               // 2 MiB user block
    public const ulong BlockAlign = 0x200000;
    // Frames are never handed out from [0x400000,0x600000): that 2 MiB window is
    // pd[2] in every address space, so the kernel cannot address a fresh block
    // through its physical address there (VA != phys inside the user window).
    public const ulong ReservedLo = 0x400000;
    public const ulong ReservedHi = 0x600000;

    // GDT selectors (64-bit flat, base 0)
    public const ushort KernelDataSel = 0x10;
    public const ushort UserCodeSel = 0x18;       // RPL3 -> 0x1B in CS
    public const ushort UserDataSel = 0x20;       // RPL3 -> 0x23 in SS/DS
    public const ushort TssSel = 0x28;

    // RTC CMOS
    public const byte RtcIndex = 0x70;            // CMOS register select
    public const byte RtcData = 0x71;             // CMOS data port

    // ATA PIO (IDE)
    // Primary IDE: data 0x1F0, error/features 0x1F1, sector count 0x1F2,
    // LBA low/mid/high 0x1F3-0x1F5, drive/head 0x1F6, status/command 0x1F7
    public const ushort AtaPrimaryData = 0x1F0;
    public const ushort AtaPrimaryError = 0x1F1;
    public const ushort AtaPrimarySecCount = 0x1F2;
    public const ushort AtaPrimaryLbaLow = 0x1F3;
    public const ushort AtaPrimaryLbaMid = 0x1F4;
    public const ushort AtaPrimaryLbaHigh = 0x1F5;
    public const ushort AtaPrimaryDriveHead = 0x1F6;
    public const ushort AtaPrimaryStatus = 0x1F7;
    public const ushort AtaPrimaryCommand = 0x1F7;
    public const byte AtaCmdReadSectors = 0x20;
    public const byte AtaCmdWriteSectors = 0x30;
    public const byte AtaCmdIdentify = 0xEC;
    public const byte AtaStatusBusy = 0x80;
    public const byte AtaStatusDrq = 0x08;
    public const byte AtaStatusReady = 0x40;

    // ext4
    public const int Ext4BlockSize = 4096;

    // Inode field offsets (ext4)
    public const int InoMode = 0, InoUid = 2, InoSizeLo = 4, InoMtime = 16,
                     InoGid = 24, InoLinks = 26, InoBlocksLo = 28, InoFlags = 32,
                     InoBlock = 40, InoSizeHi = 108, InoBlocksHi = 116;

    // ext4 directory entry (ext4_dir_entry_2)
    public const int DirIno = 0, DirRecLen = 4, DirNameLen = 6, DirType = 7, DirName = 8;

    // Environment
    public const int MaxEnvVars = 32;
    public const int EnvNameBufSize = 512;
    public const int EnvValueBufSize = 4096;

    // Shell history (fixed ring, no managed arrays)
    public const int MaxHistory = 256;
    public const int HistoryLineCap = 128;

    // Scheduler
    public const int SlotConsole = 0;             // the shell / console (PID 1)
    public const int TaskCount = 8;               // console + 7 process slots
    public const int TaskFree = 0;                // slot not in use
    public const int TaskReady = 1;               // runnable (or running)
    public const int TaskZombie = 2;              // exited, not yet reaped
    public const int KindNone = 0;
    public const int KindShell = 1;
    public const int KindDemo = 2;
    public const int KindUser = 3;                // runs in ring 3
    public const int StackSize = 16384;
    public const int MaxName = 12;

    // Built-in ring-3 programs (no filesystem yet)
    public const int ProgHello = 0;
    public const int ProgBad = 1;
    public const int ProgSpawner = 2;             // exercises INT 21h AH=80h/81h

    // Physical frame bitmap: covers the mapped 64 GiB at 4 KiB granularity
    // (2 MiB of bitmap). One bit per frame, indexed from the allocator floor.
    public const int MaxFrames = 16777216;       // 64 GiB / 4 KiB
}