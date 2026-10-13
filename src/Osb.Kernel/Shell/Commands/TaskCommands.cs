// Process commands: SPAWN/RING3, DEMO, TASK/PS, KILL, WAIT, TAB. Starting and
// waiting for jobs goes through JobControl; this file only parses arguments
// and prints the reports.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Processes;
using Osb.Kernel.Shell;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class TaskCommands
{
    // SPAWN [BAD|SPAWNER] [&]  (RING3 is a synonym)
    public static void Spawn(int prog, bool background)
    {
        JobControl.StartProgram(prog, background);
    }

    // DEMO [&]
    public static void Demo(bool background)
    {
        JobControl.StartDemo(background);
    }

    // A simple "ps": pid, name, state, memory and foreground/background.
    public static void List()
    {
        Vga.WriteLine(" PID  NAME        STATE    MEMORY      ROLE");
        for (int i = 0; i < Scheduler.Count; i++)
        {
            if (i != Scheduler.Console && Scheduler.IsFree(i)) continue;
            TaskInfo t = Scheduler.Get(i);
            ShellText.WriteDecPad((ulong)t.Pid, 4);
            Vga.PutChar(' ');
            WriteNamePad(t.Name, t.NameLen, 10);
            Vga.PutChar(' ');
            Vga.Write(t.State == Layout.TaskZombie ? "zombie   " : "run      ");
            if (t.UserBlock != 0) Vga.WriteHex(t.UserBlock);
            else Vga.Write("kernel    ");
            Vga.PutChar(' ');
            if (i == Scheduler.Console) Vga.WriteLine("fg");
            else Vga.WriteLine(t.State == Layout.TaskZombie ? "-" : (i == Scheduler.Foreground ? "fg" : "bg"));
        }
    }

    // KILL <pid>
    public static void Kill(char* line, int len)
    {
        int pid = ShellText.ParseIntAt(line, 4, len);
        int slot;
        if (pid == 0 || !Scheduler.TryFindPid(pid, out slot)) { Vga.WriteLine("No such process."); return; }
        if (slot == Scheduler.Console) { Vga.WriteLine("Cannot kill the console."); return; }
        Scheduler.Release(slot);
        Vga.Write("Killed pid ");
        Vga.WriteDec((ulong)pid);
        Vga.WriteLine(".");
    }

    // WAIT <pid>
    public static void Wait(char* line, int len)
    {
        int pid = ShellText.ParseIntAt(line, 4, len);
        if (pid == 0) { Vga.WriteLine("Usage: WAIT <pid>"); return; }
        Vga.Write("Waiting for pid ");
        Vga.WriteDec((ulong)pid);
        Vga.WriteLine(" ...");
        int code = Scheduler.WaitFor(pid);
        if (code < 0) { Vga.WriteLine("No such process (or already gone)."); return; }
        Vga.Write("Process ");
        Vga.WriteDec((ulong)pid);
        Vga.Write(" exited, code ");
        Vga.WriteDec((ulong)code);
        Vga.WriteLine(".");
    }

    // TAB <pid> - bring a running process to the foreground
    public static void Tab(char* line, int len)
    {
        int pid = ShellText.ParseIntAt(line, 3, len);
        int slot;
        if (pid == 0 || !Scheduler.TryFindPid(pid, out slot)) { Vga.WriteLine("No such process."); return; }
        if (slot == Scheduler.Console) { Vga.WriteLine("The console is already in the foreground."); return; }
        if (!Scheduler.IsReady(slot)) { Vga.WriteLine("Process is not running."); return; }
        Scheduler.MakeForeground(slot);
        JobControl.WaitForeground(slot);
    }

    private static void WriteNamePad(byte* name, int nameLen, int width)
    {
        for (int i = 0; i < nameLen; i++) Vga.PutChar((char)name[i]);
        for (int i = nameLen; i < width; i++) Vga.PutChar(' ');
    }
}