// Per-process address spaces.
//
// A process gets a private 2 MiB physical block holding its program image,
// mapped at VA 0x400000. Its page tables copy the kernel's PDPT entries, so
// the 64 GiB identity map stays shared; only GiB 0 gets a private page
// directory whose entry 2 is retargeted at the process block. Everything
// else is shared, so a process cannot see or corrupt another process's
// memory. CR3 switches happen in Scheduler.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Processes;

internal static unsafe class AddressSpace
{
    // Results of the last Create call (avoids out-parameters).
    private static ulong _block;     // 2 MiB user block (phys)
    private static ulong _cr3;       // PML4 of the new address space
    private static ulong _pt;        // 2 page-table frames (PML4 + PDPT, phys)
    private static ulong _pd0;       // private page directory for GiB 0 (phys)

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
        ulong pml4 = pt, pdpt = pt + 4096;
        ulong* P = (ulong*)pml4;
        ulong* D = (ulong*)pdpt;
        for (int i = 0; i < 512; i++) { P[i] = 0; D[i] = 0; }

        // The kernel identity-maps 64 GiB as pdpt[i] -> pd_pool[i] (one page
        // directory per GiB). Copy those PDPT entries verbatim: the page
        // directories themselves stay shared with the kernel, so a process
        // costs 2 tables instead of 2 + 64, and the kernel half stays
        // read-only from ring 3 (every PD entry has U/S=0).
        byte* masterPdpt = (byte*)NativeMethods.osb_master_pdpt();
        ulong* M = (ulong*)masterPdpt;
        int n = NativeMethods.osb_pd_count();
        if (n > 512) n = 512;
        for (int i = 0; i < n; i++) D[i] = M[i];

        // Private user block at VA 0x400000 (2 MiB, U/S=1): take it over
        // from the shared pd_pool[0] copy this process inherited above.
        // pd_pool[0][2] is the 2 MiB entry for that range; we must not
        // mutate the shared one, so give the process its own page directory
        // for GiB 0 and retarget just that entry.
        ulong pd0 = PhysicalMemory.Alloc(1, Layout.FrameSize);
        if (pd0 == 0) return false;
        ulong* T = (ulong*)pd0;
        ulong* K = (ulong*)NativeMethods.osb_master_pd();
        for (int i = 0; i < 512; i++) T[i] = K[i];
        T[2] = block | 0x87;                 // present, rw, user, PS (2 MiB)
        D[0] = pd0 | 0x7;                    // GiB 0 now private

        P[0] = pdpt | 0x7;                   // present, rw, user

        _block = block; _cr3 = pml4; _pt = pt; _pd0 = pd0;
        return true;
    }

    // Release the frames backing a process address space.
    public static void Release(ulong block, ulong pageTables)
    {
        if (block != 0) PhysicalMemory.Free(block, Layout.BlockFrames);
        if (pageTables != 0) PhysicalMemory.Free(pageTables, 2);
        if (_pd0 != 0) { PhysicalMemory.Free(_pd0, 1); _pd0 = 0; }
    }
}