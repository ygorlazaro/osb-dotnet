// INT 21h: the DOS-style system-call gate a ring-3 program calls instead of a
// native syscall instruction. Function number in AH, arguments in RSI/RDI/...,
// result written back to RAX in the saved frame.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Processes;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Cpu;

internal static unsafe class Syscalls
{
    public static void Handle(ulong* frame)
    {
        ulong rax = frame[14];           // full RAX as passed by the user
        ulong rsi = frame[10];           // arg0 (e.g. string pointer)
        ulong rdi = frame[9];            // arg1
        ulong rdx = frame[11];           // arg2
        ulong rcx = frame[12];           // arg3
        ulong func = (rax >> 8) & 0xFF;  // AH = function number

        if (func == 0x09) // print '$'-terminated string at RSI
        {
            if (rsi < Layout.UserBase || rsi >= Layout.UserLimit)
            {
                frame[14] = unchecked((ulong)-1); // EFAULT
                return;
            }
            byte* p = (byte*)rsi;
            int n = 0;
            while (n < 4096 && p[n] != (byte)'$')
            {
                Vga.PutChar((char)p[n]);
                n++;
            }
            frame[14] = 0;
            return;
        }

        if (func == 0x30) // getpid: current process id in EAX
        {
            frame[14] = (ulong)Scheduler.Get(Scheduler.Current).Pid;
            return;
        }

        if (func == 0x4C) // exit(code in AL): become a zombie; wait() reaps us
        {
            Scheduler.ExitCurrent((int)(rax & 0xFF));
            return;
        }

        if (func == 0x80) // spawn(program id in AL): returns the child's pid, or -1
        {
            int prog = (int)(rax & 0xFF);
            int pid;
            SpawnResult r = Scheduler.SpawnUser(prog, Scheduler.Current, out pid);
            frame[14] = unchecked((ulong)(r == SpawnResult.Ok ? pid : -1));
            return;
        }

        if (func == 0x81) // wait(pid in RSI): blocks until the child exits
        {
            frame[14] = unchecked((ulong)Scheduler.WaitFor((int)rsi));
            return;
        }

        if (func == 0x01) // write(buf in RDI, len in RSI): bytes to the console
        {
            if (rdi < Layout.UserBase || rdi >= Layout.UserLimit)
            {
                frame[14] = unchecked((ulong)-1); // EFAULT
                return;
            }
            byte* p = (byte*)rdi;
            ulong n = rsi > 4096 ? 4096 : rsi;
            for (ulong i = 0; i < n; i++) Vga.PutChar((char)p[i]);
            frame[14] = n;
            return;
        }

        if (func == 0x10) // meminfo(out in RDI): [u64 totalKB, usedKB, freeKB]
        {
            if (rdi < Layout.UserBase || rdi >= Layout.UserLimit - 24)
            {
                frame[14] = unchecked((ulong)-1);
                return;
            }
            ulong* o = (ulong*)rdi;
            ulong total = PhysicalMemory.Total * Layout.FrameSize / 1024UL;
            ulong used = PhysicalMemory.Used * Layout.FrameSize / 1024UL;
            o[0] = total;
            o[1] = used;
            o[2] = total > used ? total - used : 0;
            frame[14] = 1;
            return;
        }

        if (func == 0x20) // resolve(path in RDI, len in RSI, out in RDX)
        {
            // out: [u32 ino, u16 mode, u16 uid, u64 size]
            if (rdi < Layout.UserBase || rdx < Layout.UserBase || rdx >= Layout.UserLimit - 16)
            {
                frame[14] = 0;
                return;
            }
            if (!Ext4.Mount()) { frame[14] = 0; return; }
            uint ino;
            if (!Ext4.Resolve((char*)rdi, (int)rsi, out ino)) { frame[14] = 0; return; }
            byte* inode = stackalloc byte[256];
            if (!Ext4.ReadInode(ino, inode)) { frame[14] = 0; return; }
            byte* o = (byte*)rdx;
            Wr32(o, 0, ino);
            Wr16(o, 4, Ext4.InodeGetMode(inode));
            Wr16(o, 6, (ushort)Ext4.InodeGetUid(inode));
            ulong sz = Ext4.InodeGetSize(inode);
            for (int k = 0; k < 8; k++) o[8 + k] = (byte)(sz >> (k * 8));
            frame[14] = 1;
            return;
        }

        if (func == 0x21) // dirnext(dir ino in RDI, index in RSI, out in RDX, cap in RCX)
        {
            // out: [u32 childIno, u8 type, u8 nameLen, name bytes...]; "." and
            // ".." are skipped. Returns 1 while entries remain, 0 at the end.
            if (rdi < Layout.UserBase || rdx < Layout.UserBase || rdx >= Layout.UserLimit - 8)
            {
                frame[14] = 0;
                return;
            }
            if (!Ext4.Mount()) { frame[14] = 0; return; }
            byte* din = stackalloc byte[256];
            if (!Ext4.ReadInode((uint)rdi, din)) { frame[14] = 0; return; }
            if (!Ext4.IsDir(Ext4.InodeGetMode(din))) { frame[14] = 0; return; }
            uint size = Ext4.InodeGetSize(din);
            ulong blocks = ((ulong)size + (ulong)Ext4.BlockSize - 1) / (ulong)Ext4.BlockSize;
            if (blocks == 0) blocks = 1;
            byte* blk = stackalloc byte[Ext4.BlockSize];
            ulong seen = 0;
            for (ulong b = 0; b < blocks; b++)
            {
                uint bnum = Ext4.InodeGetBlock(din, b);
                if (bnum == 0) continue;
                Ext4.ReadBlock(bnum, blk);
                int off = 0;
                while (off + 8 <= Ext4.BlockSize)
                {
                    byte* de = blk + off;
                    uint iino = Ext4.DirEntryInode(de);
                    ushort rlen = Ext4.DirEntryRecLen(de);
                    if (rlen == 0) break;
                    byte nlen = Ext4.DirEntryNameLen(de);
                    if (iino != 0 && nlen > 0)
                    {
                        bool dotty = (nlen == 1 && de[Layout.DirName] == (byte)'.') ||
                                     (nlen == 2 && de[Layout.DirName] == (byte)'.' &&
                                                      de[Layout.DirName + 1] == (byte)'.');
                        if (!dotty)
                        {
                            if (seen == rsi)
                            {
                                byte* o = (byte*)rdx;
                                Wr32(o, 0, iino);
                                o[4] = Ext4.DirEntryType(de);
                                o[5] = nlen;
                                ulong cap = rcx > 240 ? 240 : rcx;   // leave room in the block
                                for (ulong k = 0; k < nlen && k + 6 < cap; k++)
                                    o[6 + k] = de[Layout.DirName + k];
                                frame[14] = 1;
                                return;
                            }
                            seen++;
                        }
                    }
                    off += rlen;
                    if (off > Ext4.BlockSize - 8) break;
                }
            }
            frame[14] = 0;   // end of directory
            return;
        }

        if (func == 0x22) // getcwd(buf in RDI, cap in RSI) -> length
        {
            if (rdi < Layout.UserBase || rdi >= Layout.UserLimit)
            {
                frame[14] = unchecked((ulong)-1);
                return;
            }
            byte* cwd = stackalloc byte[160];
            int cl = Storage.Cwd.CopyTo(cwd, 160);
            byte* o = (byte*)rdi;
            ulong cap = rsi > 256 ? 256 : rsi;
            int n = cl < (int)cap - 1 ? cl : (int)cap - 1;
            for (int k = 0; k < n; k++) o[k] = cwd[k];
            o[n] = 0;
            frame[14] = (ulong)n;
            return;
        }

        if (func == 0x23) // getargs(buf in RDI, cap in RSI): the argument string
        {
            if (rdi < Layout.UserBase || rdi >= Layout.UserLimit)
            {
                frame[14] = unchecked((ulong)-1);
                return;
            }
            int n = Scheduler.GetArgs(Scheduler.Current, (byte*)rdi, (int)(rsi > 128 ? 128 : rsi));
            frame[14] = (ulong)n;
            return;
        }

        frame[14] = unchecked((ulong)-2); // ENOSYS
    }

    private static void Wr32(byte* p, int o, uint v)
    {
        p[o] = (byte)(v & 0xFF);
        p[o + 1] = (byte)((v >> 8) & 0xFF);
        p[o + 2] = (byte)((v >> 16) & 0xFF);
        p[o + 3] = (byte)((v >> 24) & 0xFF);
    }

    private static void Wr16(byte* p, int o, ushort v)
    {
        p[o] = (byte)(v & 0xFF);
        p[o + 1] = (byte)((v >> 8) & 0xFF);
    }
}