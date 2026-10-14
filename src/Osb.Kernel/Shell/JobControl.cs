// Foreground / background jobs as the shell sees them: starting a process,
// printing what it got, and blocking until it is done (or Ctrl+C'd).

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Processes;

namespace Osb.Kernel.Shell;

internal static unsafe class JobControl
{
    // Start a DEMO job (kernel task; no user address space). Without '&' it
    // becomes the foreground job and the shell waits; with '&' the prompt returns.
    public static void StartDemo(bool background)
    {
        int slot = Scheduler.StartDemo();
        if (slot < 0) { Vga.WriteLine("No free task slot."); return; }
        TaskInfo t = Scheduler.Get(slot);
        Vga.Write("Started DEMO [");
        Vga.WriteDec((ulong)t.Pid);
        Vga.WriteLine(background ? "] in the background." : "] in the foreground.");
        if (!background) { Scheduler.MakeForeground(slot); WaitForeground(slot); }
    }

    // Start a built-in ring-3 program. Without '&' it becomes the foreground
    // job and the shell waits; with '&' the prompt returns.
    public static void StartProgram(int prog, bool background)
    {
        int pid;
        SpawnResult r = Scheduler.SpawnUser(prog, Scheduler.Console, out pid);
        if (r == SpawnResult.NoSlot) { Vga.WriteLine("No free task slot."); return; }
        if (r != SpawnResult.Ok) { Vga.WriteLine("Out of memory."); return; }
        int slot;
        if (!Scheduler.TryFindPid(pid, out slot)) return;

        TaskInfo t = Scheduler.Get(slot);
        Vga.Write("Spawned ");
        Vga.Write(Scheduler.ProgramName(prog));
        Vga.Write(" [");
        Vga.WriteDec((ulong)pid);
        Vga.Write(background ? "] in the background" : "] in the foreground");
        Vga.Write(" (block ");
        Vga.WriteHex(t.UserBlock);
        Vga.WriteLine(").");
        if (!background) { Scheduler.MakeForeground(slot); WaitForeground(slot); }
    }

    // Start an external program (an OSBEXE file on A:, e.g. A:\OSB\BIN\MEM.EXE).
    // args is the raw argument string for the program; without '&' the shell
    // waits for it to finish.
    public static void StartFile(char* path, int pathLen, char* args, int argsLen, bool background)
    {
        int pid;
        SpawnResult r = Scheduler.SpawnFile(path, pathLen, Scheduler.Console, out pid);
        if (r == SpawnResult.NotFound) { Vga.WriteLine("Bad command or file name."); return; }
        if (r == SpawnResult.BadImage) { Vga.WriteLine("Not a valid executable."); return; }
        if (r == SpawnResult.NoSlot) { Vga.WriteLine("No free task slot."); return; }
        if (r == SpawnResult.OutOfMemory) { Vga.WriteLine("Out of memory."); return; }

        int slot;
        if (!Scheduler.TryFindPid(pid, out slot)) return;
        Scheduler.SetArgs(slot, args, argsLen);
        if (!background) { Scheduler.MakeForeground(slot); WaitForeground(slot); }
    }

    // Block until the foreground job ends (zombie), is aborted (Ctrl+C), or is
    // otherwise gone. A zombie is reaped here and its exit code shown.
    public static void WaitForeground(int slot)
    {
        Vga.Write("Foreground [");
        Vga.WriteDec((ulong)Scheduler.Get(slot).Pid);
        Vga.WriteLine("]  (Ctrl+C returns to the shell)");
        bool aborted = false;
        while (Scheduler.IsReady(slot))
        {
            if (ConsoleControl.TakeCtrlC()) { aborted = true; break; }  // set by the IRQ
            NativeMethods.halt();
        }
        Vga.PutChar('\n');
        if (aborted)
        {
            Scheduler.Release(slot);
            Vga.WriteLine("Foreground job aborted (Ctrl+C).");
        }
        else if (Scheduler.IsZombie(slot))
        {
            int code = Scheduler.Reap(slot);
            Vga.Write("Job finished, exit code ");
            Vga.WriteDec((ulong)(code & 0xFF));
            Vga.WriteLine(".");
        }
        else
        {
            Vga.WriteLine("Job ended.");
        }
        Scheduler.Foreground = -1;
    }
}