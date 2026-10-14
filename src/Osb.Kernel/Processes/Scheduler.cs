// Preemptive round-robin scheduler over a fixed table of task slots.
//
// Slot 0 is the console/shell (PID 1, kernel address space); the others are
// processes/jobs, each with its own kernel stack. A ring-3 process also owns
// a private address space (CR3) and a 2 MiB user block. The switch itself is
// done in assembly (osb_switch) and is only ever driven from the timer IRQ.

using System.Runtime;
using Osb.Kernel.Cpu;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Processes;

// Outcome of Scheduler.SpawnUser, so the caller can explain the refusal.
internal enum SpawnResult
{
    Ok,
    NoSlot,          // every process slot is in use
    OutOfMemory,     // no room for a private 2 MiB block
    NotFound,        // no such file on A:
    BadImage         // not a valid OSBEXE image
}

// Read-only snapshot of one task slot, for the "ps" listing and job control.
internal unsafe struct TaskInfo
{
    public int Pid;
    public int State;
    public ulong UserBlock;      // 2 MiB user block (phys), 0 for kernel tasks
    public byte* Name;           // ASCII, NUL-terminated
    public int NameLen;
}

internal static unsafe class Scheduler
{
    public const int Console = Layout.SlotConsole;   // the shell / console (PID 1)
    public const int Count = Layout.TaskCount;       // console + process slots

    // Argument string capacity for external programs (per slot).
    public const int ArgsCap = 96;

    private unsafe struct TaskTable
    {
        public fixed ulong sp[Count];             // saved kernel SP (osb_switch)
        public fixed int pid[Count];
        public fixed int ppid[Count];
        public fixed int state[Count];
        public fixed int kind[Count];
        public fixed int fg[Count];               // 1 = foreground job
        public fixed int exitCode[Count];
        public fixed ulong arg[Count];            // kind-specific (user entry RIP)
        public fixed ulong cr3[Count];            // address space (page table root)
        public fixed ulong ublock[Count];         // 2 MiB user block (phys), 0 if none
        public fixed ulong ptphys[Count];         // 3 page-table frames (phys), 0 if none
        public fixed ulong round[Count];          // demo: rounds printed
        public fixed ulong deadline[Count];       // demo: next print tick
        public fixed byte name[Count * Layout.MaxName];
        public fixed byte args[Count * ArgsCap];  // external programs: argument string
    }

    private unsafe struct StackPool { public fixed byte bytes[Count * Layout.StackSize]; }

    private static TaskTable _tasks;
    private static StackPool _stacks;
    private static int _current;                  // slot currently on the CPU
    private static int _nextPid;                  // next pid to hand out
    private static int _foreground;               // foreground job slot, or -1

    // Slot currently on the CPU.
    public static int Current
    {
        get => _current;
        set => _current = value;
    }

    // Foreground job slot, or -1 when the shell owns the console.
    public static int Foreground
    {
        get => _foreground;
        set => _foreground = value;
    }

    public static void Init()
    {
        for (int i = 0; i < Count; i++)
        {
            _tasks.state[i] = Layout.TaskFree;
            _tasks.kind[i] = Layout.KindNone;
            _tasks.fg[i] = 0;
            _tasks.pid[i] = 0;
            _tasks.ppid[i] = 0;
            _tasks.exitCode[i] = 0;
            _tasks.sp[i] = 0;
            _tasks.cr3[i] = AddressSpace.Master;
            _tasks.ublock[i] = 0;
            _tasks.ptphys[i] = 0;
        }
        // Slot 0 is the shell/console, running on the boot stack in the kernel
        // address space. Its saved SP is captured by the first osb_switch out.
        _tasks.state[Console] = Layout.TaskReady;
        _tasks.kind[Console] = Layout.KindShell;
        _tasks.pid[Console] = 1;
        SetName(Console, "CONSOLE");
        _current = Console;
        _nextPid = 2;
        _foreground = -1;
    }

    // Snapshot of a slot (name points into the table: copy it out before the
    // scheduler reuses the slot).
    public static TaskInfo Get(int slot)
    {
        TaskInfo t;
        t.Pid = _tasks.pid[slot];
        t.State = _tasks.state[slot];
        t.UserBlock = _tasks.ublock[slot];
        fixed (byte* p = _tasks.name)
        {
            t.Name = p + slot * Layout.MaxName;
        }
        int n = 0;
        while (n < Layout.MaxName && t.Name[n] != 0) n++;
        t.NameLen = n;
        return t;
    }

    public static bool IsFree(int slot) { return _tasks.state[slot] == Layout.TaskFree; }
    public static bool IsReady(int slot) { return _tasks.state[slot] == Layout.TaskReady; }
    public static bool IsZombie(int slot) { return _tasks.state[slot] == Layout.TaskZombie; }

    // Find a live (ready) or zombie slot by pid; false if unknown.
    public static bool TryFindPid(int pid, out int slot)
    {
        for (int i = 0; i < Count; i++)
            if (_tasks.pid[i] == pid && (i == Console || _tasks.state[i] != Layout.TaskFree))
            {
                slot = i;
                return true;
            }
        slot = -1;
        return false;
    }

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

    // Top (16-byte aligned) of a slot's kernel stack: what the TSS RSP0 needs.
    public static ulong KernelStackTop(int slot)
    {
        fixed (byte* p = _stacks.bytes)
            return ((ulong)p + (ulong)slot * Layout.StackSize + Layout.StackSize) & ~(ulong)15;
    }

    private static void SetName(int slot, string name)
    {
        int o = slot * Layout.MaxName;
        for (int i = 0; i < Layout.MaxName; i++) _tasks.name[o + i] = 0;
        int n = name.Length; if (n > Layout.MaxName - 1) n = Layout.MaxName - 1;
        fixed (char* p = name)
        {
            for (int i = 0; i < n; i++) _tasks.name[o + i] = (byte)p[i];
        }
    }

    // Name from a char buffer (external commands carry no managed string).
    private static void SetNameChars(int slot, char* name, int len)
    {
        int o = slot * Layout.MaxName;
        for (int i = 0; i < Layout.MaxName; i++) _tasks.name[o + i] = 0;
        if (len > Layout.MaxName - 1) len = Layout.MaxName - 1;
        for (int i = 0; i < len; i++) _tasks.name[o + i] = (byte)name[i];
    }

    // The argument string handed to an external program (read back by the
    // AH=23h syscall). Trailing spaces are trimmed.
    public static void SetArgs(int slot, char* args, int len)
    {
        int o = slot * ArgsCap;
        for (int i = 0; i < ArgsCap; i++) _tasks.args[o + i] = 0;
        while (len > 0 && args[len - 1] == ' ') len--;
        if (len > ArgsCap - 1) len = ArgsCap - 1;
        for (int i = 0; i < len; i++) _tasks.args[o + i] = (byte)args[i];
    }

    // Copies the current task's argument string out (0-terminated).
    public static int GetArgs(int slot, byte* dst, int cap)
    {
        int o = slot * ArgsCap;
        int n = 0;
        while (n < ArgsCap - 1 && _tasks.args[o + n] != 0 && n < cap - 1)
        {
            dst[n] = _tasks.args[o + n];
            n++;
        }
        dst[n] = 0;
        return n;
    }

    private static int AllocSlot()
    {
        for (int i = Console + 1; i < Count; i++)
            if (_tasks.state[i] == Layout.TaskFree) return i;   // skip zombies (not yet reaped)
        return -1;
    }

    private static void SeedSlot(int slot, ulong entry, string name, int kind)
    {
        ulong sp;
        fixed (byte* p = _stacks.bytes)
            sp = SeedTask(p + slot * Layout.StackSize, Layout.StackSize, entry);
        _tasks.sp[slot] = sp;
        _tasks.pid[slot] = _nextPid++;
        _tasks.ppid[slot] = _tasks.pid[_current];
        _tasks.state[slot] = Layout.TaskReady;
        _tasks.kind[slot] = kind;
        _tasks.fg[slot] = 0;
        _tasks.exitCode[slot] = 0;
        _tasks.arg[slot] = 0;
        _tasks.cr3[slot] = AddressSpace.Master;
        _tasks.ublock[slot] = 0;
        _tasks.ptphys[slot] = 0;
        _tasks.round[slot] = 0;
        _tasks.deadline[slot] = 0;
        SetName(slot, name);
    }

    // Start a kernel job (the demo task). Returns its slot, or -1.
    public static int StartDemo()
    {
        int slot = AllocSlot();
        if (slot < 0) return -1;
        SeedSlot(slot, NativeMethods.osb_sched_get_demo_entry(), "DEMO", Layout.KindDemo);
        return slot;
    }

    // Create a process running a built-in ring-3 program, in its own address
    // space. Returns Ok and the new pid; does not wait for the child.
    public static SpawnResult SpawnUser(int prog, int parentSlot, out int pid)
    {
        pid = -1;
        int slot = AllocSlot();
        if (slot < 0) return SpawnResult.NoSlot;

        ulong src, end;
        if (prog == Layout.ProgBad) { src = NativeMethods.osb_userbad_begin(); end = NativeMethods.osb_userbad_end(); }
        else if (prog == Layout.ProgSpawner) { src = NativeMethods.osb_spawner_begin(); end = NativeMethods.osb_spawner_end(); }
        else { src = NativeMethods.osb_userstub_begin(); end = NativeMethods.osb_userstub_end(); }

        if (!AddressSpace.Create(src, end)) return SpawnResult.OutOfMemory;

        SeedSlot(slot, NativeMethods.osb_sched_get_user_entry(), ProgramName(prog), Layout.KindUser);
        _tasks.arg[slot] = Layout.UserBase;                 // entry RIP in the private block
        _tasks.cr3[slot] = AddressSpace.Cr3;
        _tasks.ublock[slot] = AddressSpace.Block;
        _tasks.ptphys[slot] = AddressSpace.PageTables;
        _tasks.ppid[slot] = _tasks.pid[parentSlot];
        pid = _tasks.pid[slot];
        return SpawnResult.Ok;
    }

    public static string ProgramName(int prog)
    {
        if (prog == Layout.ProgBad) return "BAD";
        if (prog == Layout.ProgSpawner) return "SPAWNER";
        return "HELLO";
    }

    // ---- external commands (A:\OSB\BIN\*.EXE) ----

    // The OSBEXE image is staged here before AddressSpace copies it into the
    // private block. Only the console spawns, so one buffer is enough.
    private unsafe struct ImgBuf { public fixed byte data[16384]; }
    private static ImgBuf _img;

    // Load and start an executable file from the ext4 image. The file starts
    // with the 16-byte OSBEXE header ("OSBX", version, entry offset, image
    // size); the code lands at UserBase and the process enters ring 3 at
    // UserBase + entry. Returns why it refused, for the shell to explain.
    public static SpawnResult SpawnFile(char* path, int pathLen, int parentSlot, out int pid)
    {
        pid = -1;
        if (!Ext4.Mount()) return SpawnResult.NotFound;
        uint ino;
        if (!Ext4.Resolve(path, pathLen, out ino)) return SpawnResult.NotFound;
        byte* inode = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, inode)) return SpawnResult.NotFound;
        if (!Ext4.IsReg(Ext4.InodeGetMode(inode))) return SpawnResult.NotFound;

        byte* buf = ImgData();
        int got = 0;
        if (!Ext4.ReadFileAll(ino, buf, 16384, out got) || got < 16) return SpawnResult.BadImage;

        // header: magic "OSBX" | u16 version | u16 header size | u32 entry | u32 size
        if (buf[0] != 'O' || buf[1] != 'S' || buf[2] != 'B' || buf[3] != 'X') return SpawnResult.BadImage;
        uint version = (uint)(buf[4] | (buf[5] << 8));
        if (version != 1) return SpawnResult.BadImage;
        uint entryOff = (uint)(buf[8] | (buf[9] << 8) | (buf[10] << 16) | ((uint)buf[11] << 24));
        uint imgSize = (uint)(buf[12] | (buf[13] << 8) | (buf[14] << 16) | ((uint)buf[15] << 24));
        if (imgSize == 0 || 16 + imgSize > (uint)got) return SpawnResult.BadImage;
        if (entryOff >= imgSize) return SpawnResult.BadImage;

        if (!AddressSpace.CreateBuf(buf + 16, (int)imgSize)) return SpawnResult.OutOfMemory;

        int slot = AllocSlot();
        if (slot < 0) return SpawnResult.NoSlot;

        // the task name is the file's base name, like the shell's "MEM.EXE"
        char* nm = stackalloc char[16];
        int nl = 0;
        int start = 0;
        for (int k = pathLen - 1; k >= 0; k--)
        {
            if (path[k] == '\\' || path[k] == '/') { start = k + 1; break; }
        }
        for (int k = start; k < pathLen && nl < 15; k++) nm[nl++] = path[k];

        SeedSlot(slot, NativeMethods.osb_sched_get_user_entry(), "USER", Layout.KindUser);
        SetNameChars(slot, nm, nl);
        _tasks.arg[slot] = Layout.UserBase + entryOff;   // user RIP in the private block
        _tasks.cr3[slot] = AddressSpace.Cr3;
        _tasks.ublock[slot] = AddressSpace.Block;
        _tasks.ptphys[slot] = AddressSpace.PageTables;
        _tasks.ppid[slot] = _tasks.pid[parentSlot];
        pid = _tasks.pid[slot];
        return SpawnResult.Ok;
    }

    private static byte* ImgData()
    {
        fixed (byte* p = _img.data) { return p; }
    }

    // Force-terminate a slot: release its address space and free the slot. Used
    // by KILL and by Ctrl+C on a foreground job.
    public static void Release(int slot)
    {
        if (slot <= Console) return;
        AddressSpace.Release(_tasks.ublock[slot], _tasks.ptphys[slot]);
        _tasks.ublock[slot] = 0;
        _tasks.ptphys[slot] = 0;
        _tasks.cr3[slot] = AddressSpace.Master;
        _tasks.state[slot] = Layout.TaskFree;
        _tasks.kind[slot] = Layout.KindNone;
        _tasks.fg[slot] = 0;
        SetName(slot, "free");
        if (_foreground == slot) _foreground = -1;
    }

    // Reap a zombie: collect its exit code, then release everything.
    public static int Reap(int slot)
    {
        int code = _tasks.exitCode[slot];
        Release(slot);
        return code;
    }

    // The running task leaves: it becomes a zombie (wait/reap collects it) and
    // the scheduler switches away for good. Never returns.
    public static void ExitCurrent(int code)
    {
        int slot = _current;
        _tasks.exitCode[slot] = code;
        _tasks.state[slot] = Layout.TaskZombie;
        _tasks.fg[slot] = 0;
        if (_foreground == slot) _foreground = -1;
        Preempt();
        for (;;) NativeMethods.halt();
    }

    // Mark a job as the foreground one (the shell waits for it).
    public static void MakeForeground(int slot)
    {
        _foreground = slot;
        _tasks.fg[slot] = 1;
    }

    // wait(pid): block until the process exits, then reap it. Returns the exit
    // code, or -1 if the pid is unknown / already gone.
    public static int WaitFor(int pid)
    {
        int slot;
        if (!TryFindPid(pid, out slot)) return -1;
        while (_tasks.state[slot] == Layout.TaskReady) NativeMethods.halt();
        if (_tasks.state[slot] == Layout.TaskZombie) return Reap(slot) & 0xFF;
        return -1;   // vanished (e.g. killed)
    }

    // Called from the IRQ0 handler on every tick. Round-robins to the next ready
    // task by swapping kernel stacks; the switch is atomic (interrupt gate) and
    // the interrupted ISR frame stays parked on the stopped task's stack.
    public static void Preempt()
    {
        int cur = _current;
        int next = cur;
        for (int i = 0; i < Count; i++)
        {
            next++;
            if (next >= Count) next = 0;
            if (_tasks.state[next] == Layout.TaskReady) break;
        }
        if (next == cur) return;                      // only one runnable task

        if (_tasks.kind[next] == Layout.KindUser)
            Gdt.SetKernelStack(KernelStackTop(next)); // TSS RSP0 for the next ring-3 task
        NativeMethods.osb_load_cr3(_tasks.cr3[next]);               // per-process address space
        _current = next;
        fixed (ulong* sp = _tasks.sp)
            NativeMethods.osb_switch(sp + cur, sp + next);
    }

    // Job body: demo. Runs on its slot's kernel stack, prints once per second and
    // sleeps in between. It never yields: the PIT preempts it. Persists until
    // killed or terminated.
    [RuntimeExport("osb_demo_task_main")]
    private static void DemoTaskMain()
    {
        int slot = _current;
        ulong pid = (ulong)_tasks.pid[slot];
        for (;;)
        {
            if (Pit.Ticks >= _tasks.deadline[slot])
            {
                _tasks.round[slot]++;
                Vga.Write("DEMO [");
                Vga.WriteDec(pid);
                Vga.Write("]: round ");
                Vga.WriteDec(_tasks.round[slot]);
                Vga.Write("  (tick ");
                Vga.WriteDec(Pit.Ticks);
                Vga.WriteLine(")");
                _tasks.deadline[slot] = Pit.Ticks + (ulong)Pit.Hz;   // next round in 1 s
            }
            NativeMethods.halt(); // sleep until the next interrupt; the PIT schedules the rest
        }
    }

    // Job body: ring-3 program. The scheduler has already loaded this process's
    // CR3, so VA 0x400000 is its private block. Drops to CPL3 and never comes
    // back (it exits via INT 21h or faults).
    [RuntimeExport("osb_user_task_main")]
    private static void UserTaskMain()
    {
        int slot = _current;
        NativeMethods.osb_enter_user(_tasks.arg[slot], Layout.UserStackTop);
        for (;;) NativeMethods.halt();                              // not reached
    }
}