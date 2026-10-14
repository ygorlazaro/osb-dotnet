// Commands that do not touch files or processes: identity, console, clock,
// memory report and machine control.

using Osb.Kernel.Cpu;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class SystemCommands
{
    public static void Version()
    {
        Vga.WriteLine("OSB (dotnet) version 0.1 - bootable kernel");
    }

    public static void About()
    {
        Vga.WriteLine("OSB: Operating System Basic, created in 2000, rebooted in C#.");
    }

    public static void WhoAmI()
    {
        char* nm = stackalloc char[Osb.Kernel.Shell.Users.NameCap];
        int nl = Osb.Kernel.Shell.Users.NameAt(Osb.Kernel.Shell.Users.CurrentIndex, nm);
        Vga.WriteStr(nm, nl);
        Vga.PutChar('\n');
    }

    public static unsafe void Help(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Overview(); return; }
        int s = i;
        while (i < len && line[i] != ' ') i++;
        if (CommandHelp.TryShow(line + s, i - s)) return;
        Vga.Write("No help for ");
        for (int k = s; k < i; k++) Vga.PutChar(line[k]);
        Vga.PutChar('\n');
    }

    public static void Overview()
    {
        Vga.WriteLine("Commands: VER  ABOUT  HELP  CLS  ECHO <text>  SET  TICKS");
        Vga.WriteLine("          SET [NAME=value]  env ($VAR, case-insensitive names)");
        Vga.WriteLine("Detail:   COMMAND /?        shows the help page of one command");
        Vga.WriteLine("Files:    DIR [path] [/W]  CD [path]  PWD  TYPE <file>  (ext4/A:)");
        Vga.WriteLine("          MD  RD  DEL (to trash)  REN/MOVE  COPY  SIZE  LN [/S]  COLOR  CLEAR/CLS");
        Vga.WriteLine("          TRASH [/F <pattern>]  list/search the per-user trash ($HOME\\TRASH)");
        Vga.WriteLine("          TRASH /U <name>       undelete back to the original folder");
        Vga.WriteLine("          GREP [/I] <pattern> [file]   filter lines (/I = caseless)");
        Vga.WriteLine("Pipes:    A | B        A's console output becomes B's input");
        Vga.WriteLine("          OSLANG <script.OSL> [args]  run an OSLANG script (A:)");
        Vga.WriteLine("          SLEEP   TIME  DATE  MEM  HALT  POWEROFF  REBOOT");
        Vga.WriteLine("History:  HISTORY [n]     numbered list (100 latest by default)");
        Vga.WriteLine("          (:N)            re-run entry N of the history");
        Vga.WriteLine("          Up/Down         walk the history while editing");
        Vga.WriteLine("Editing:  TAB completes commands, /flags and filenames");
        Vga.WriteLine("Users:    USER            list accounts (uid 0 = root)");
        Vga.WriteLine("          USER /ADD <name> [password]   /CHANGE  /DEL <name> [/HOME]");
        Vga.WriteLine("          USER /SUDO <name> | /UNSUDO <name>  edit the sudoers");
        Vga.WriteLine("          LOGIN [name]    switch user (passwordless for root)");
        Vga.WriteLine("          EXIT | LOGOUT   end the session, back to 'login:'");
        Vga.WriteLine("Perms:    CHOWN <user> <path>   CHMOD <octal> <path>  (root/owner)");
        Vga.WriteLine("          A:\\OSB is root-owned; HOME folders are the user's;");
        Vga.WriteLine("          A:\\OSB\\CONF holds PASSWD and SUDOERS; SUDO runs as root");
        Vga.WriteLine("Processes:SPAWN [BAD|SPAWNER] [&]  start a ring-3 process");
        Vga.WriteLine("          RING3 [BAD] [&]  synonym for SPAWN");
        Vga.WriteLine("          TASK / PS        list processes (a simple ps)");
        Vga.WriteLine("          KILL <pid>       terminate a process");
        Vga.WriteLine("          WAIT <pid>       wait for a child and collect its code");
        Vga.WriteLine("          TAB <pid>        bring a process to the foreground");
        Vga.WriteLine("          DEMO [&]         start a demo (kernel) task");
        Vga.WriteLine("          Add '&' to run in the background; Ctrl+C aborts a");
        Vga.WriteLine("          foreground job and returns to the shell.");
    }

    public static void Clear()
    {
        Vga.Clear();
    }

    // MEM: what the frame allocator is holding and what range it covers.
    public static void Memory()
    {
        Vga.Write("Physical frames: ");
        Vga.WriteDec(PhysicalMemory.Used);
        Vga.Write(" used / ");
        Vga.WriteDec(PhysicalMemory.Total);
        Vga.Write(" total  (");
        Vga.WriteDec((PhysicalMemory.Top - PhysicalMemory.Floor) / 1024);
        Vga.WriteLine(" KiB usable)");
        Vga.Write("Range: ");
        Vga.WriteHex(PhysicalMemory.Floor);
        Vga.Write(" - ");
        Vga.WriteHex(PhysicalMemory.Top);
        Vga.WriteLine("");
    }

    public static void Time()
    {
        Rtc.PrintTime();
        Vga.PutChar('\n');
    }

    public static void Date()
    {
        Rtc.PrintDate();
        Vga.PutChar('\n');
    }

    // TICKS: uptime, in seconds and in timer ticks.
    public static void Ticks()
    {
        Vga.Write("Uptime: ");
        Vga.WriteDec(Pit.Ticks / (ulong)Pit.Hz);
        Vga.Write(" s (");
        Vga.WriteDec(Pit.Ticks);
        Vga.WriteLine(" ticks)");
    }

    public static void Sleep(char* line, int len)
    {
        int i = 5;
        while (i < len && line[i] == ' ') i++;
        ulong secs = Vga.ParseUInt(line, i, len);
        Vga.WriteLine("Sleeping ...");
        ulong target = Pit.Ticks + secs * (ulong)Pit.Hz;
        while (Pit.Ticks < target) NativeMethods.halt(); // tick-driven wait; keys queue up
        Vga.WriteLine("Done.");
    }

    public static void Echo(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        for (; i < len; i++)
            Vga.PutChar(line[i]);
        Vga.PutChar('\n');
    }

    public static void Halt()
    {
        Vga.WriteLine("System halted. (CPU stopped; in QEMU press Ctrl+Alt+2 then");
        Vga.WriteLine("'quit', or use POWEROFF to close the emulator cleanly.)");
        NativeMethods.cli();
        for (;;) NativeMethods.hlt_cpu();
    }

    public static void PowerOff()
    {
        Vga.WriteLine("Powering off (ACPI S5) ...");
        NativeMethods.cli();
        NativeMethods.osb_poweroff();                 // QEMU exits; on real HW this is S5
        for (;;) NativeMethods.hlt_cpu();             // fallback if ACPI is unavailable
    }

    public static void Reboot()
    {
        Vga.WriteLine("Rebooting ...");
        NativeMethods.osb_reboot();                   // 8042 reset; never returns
        for (;;) NativeMethods.hlt_cpu();
    }
}