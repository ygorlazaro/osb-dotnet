// Per-process address spaces.
//
// A process gets a private 2 MiB physical block holding its program image,
// mapped at VA 0x400000 through a fresh copy of the kernel's page directory.
// Everything else (kernel half) stays shared, so a process cannot see or
// corrupt another process's memory. CR3 switches happen in Scheduler.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Processes;

internal static unsafe class AddressSpace
{
    // Results of the last Create call (avoids out-parameters).
    private static ulong _block;     // 2 MiB user block (phys)
    private static ulong _cr3;       // PML4 of the new address space
    private static ulong _pt;        // 3 page-table frames (phys)

    public static ulong Block => _block;
    public static ulong Cr3 => _cr3;
    public static ulong PageTables => _pt;

    // The kernel's own page table root.
    public static ulong Master => NativeMethods.osb_master_pml4();

    // Build a fresh address space for a process: copy the kernel's identity
    // mappings, then point pd[2] at a private 2 MiB block holding the program
    // image at VA 0x400000 (stack near VA 0x480000). Returns false if no memory.
    public static bool Create(ulong blobSrc, ulong blobEnd)
    {
        ulong block = PhysicalMemory.Alloc(Layout.BlockFrames, Layout.BlockAlign);
        if (block == 0) return false;
        ulong pt = PhysicalMemory.Alloc(3, Layout.FrameSize);
        if (pt == 0) { PhysicalMemory.Free(block, Layout.BlockFrames); return false; }

        byte* b = (byte*)block;
        for (ulong i = 0; i < (ulong)Layout.BlockFrames * Layout.FrameSize; i++) b[i] = 0;
        byte* s = (byte*)blobSrc;
        for (ulong i = 0; i < blobEnd - blobSrc; i++) b[i] = s[i];

        return Finish(block, pt);
    }

    // Same as Create, but the image comes from a byte buffer (a file read from
    // disk, for external A:\OSB\BIN programs).
    public static bool CreateBuf(byte* src, int len)
    {
        ulong block = PhysicalMemory.Alloc(Layout.BlockFrames, Layout.BlockAlign);
        if (block == 0) return false;
        ulong pt = PhysicalMemory.Alloc(3, Layout.FrameSize);
        if (pt == 0) { PhysicalMemory.Free(block, Layout.BlockFrames); return false; }

        byte* b = (byte*)block;
        for (ulong i = 0; i < (ulong)Layout.BlockFrames * Layout.FrameSize; i++) b[i] = 0;
        for (int i = 0; i < len; i++) b[i] = src[i];

        return Finish(block, pt);
    }

    private static bool Finish(ulong block, ulong pt)
    {
        ulong pml4 = pt, pdpt = pt + 4096, pd = pt + 8192;
        ulong* P = (ulong*)pml4;
        ulong* D = (ulong*)pdpt;
        ulong* T = (ulong*)pd;
        for (int i = 0; i < 512; i++) { P[i] = 0; D[i] = 0; }
        ulong* M = (ulong*)NativeMethods.osb_master_pd();
        for (int i = 0; i < 512; i++) T[i] = M[i];
        T[2] = block | 0x87;                 // present, rw, user, PS (2 MiB)
        P[0] = pdpt | 0x7;                   // present, rw, user
        D[0] = pd | 0x7;

        _block = block; _cr3 = pml4; _pt = pt;
        return true;
    }

    // Release the frames backing a process address space.
    public static void Release(ulong block, ulong pageTables)
    {
        if (block != 0) PhysicalMemory.Free(block, Layout.BlockFrames);
        if (pageTables != 0) PhysicalMemory.Free(pageTables, 3);
    }
}