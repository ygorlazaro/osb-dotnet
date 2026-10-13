// Ring-3 support: a flat GDT with kernel + user segments and a TSS.
// The TSS's RSP0 points at the running user task's kernel stack: the CPU
// switches to it when an interrupt/syscall arrives from ring 3.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Cpu;

internal static unsafe class Gdt
{
    private unsafe struct GdtTable { public fixed byte bytes[7 * 8]; }  // 7 descriptors
    private unsafe struct GdtrTable { public fixed byte data[10]; }
    private unsafe struct TssStruct { public fixed byte bytes[104]; }

    private static GdtTable _gdt;
    private static GdtrTable _gdtr;
    private static TssStruct _tss;

    // Build a GDT with kernel + user segments and a TSS descriptor, load it,
    // reload the data segment registers and load the task register.
    public static void Init(ulong kernelStackTop)
    {
        // --- descriptor table ---
        for (int i = 0; i < 7 * 8; i++) _gdt.bytes[i] = 0;

        WriteQuad(0x08, 0x00AF9A000000FFFF); // kernel code (DPL0)
        WriteQuad(0x10, 0x00AF92000000FFFF); // kernel data (DPL0)
        WriteQuad(0x18, 0x00AFFA000000FFFF); // user code   (DPL3)
        WriteQuad(0x20, 0x00AFF2000000FFFF); // user data   (DPL3)

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
        SetKernelStack(kernelStackTop);                        // sane default

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
            NativeMethods.osb_lgdt(p);
        }
        NativeMethods.osb_reload_segments();
        NativeMethods.osb_ltr(Layout.TssSel);
    }

    // Point the TSS RSP0 at the kernel stack the CPU must land on for the next
    // ring-0 transition (the scheduler passes the slot's stack top).
    public static void SetKernelStack(ulong stackTop)
    {
        fixed (byte* tp = _tss.bytes)
            *(ulong*)(tp + 4) = stackTop & ~(ulong)15;
    }

    private static void WriteQuad(int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
            _gdt.bytes[offset + i] = (byte)(value >> (i * 8));
    }
}