// The OSB shell: one prompt, one input line, one command at a time.
//
// Input arrives from two places and is folded into the same editing path: the
// COM1 serial console (IRQ4, how a headless -serial stdio session drives the
// shell) and the PS/2 keyboard (IRQ1). When both rings are empty the shell
// halts; the PIT handler preempts it and runs the other tasks.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Shell.Commands;

namespace Osb.Kernel.Shell;

internal static unsafe class Shell
{
    public static void Run()
    {
        Prompt.Write(); Vga.PutChar('\n');
        char* line = stackalloc char[128];
        int len = 0;
        bool shift = false;

        while (true)
        {
            if (ConsoleControl.TakeCtrlC()) { len = 0; Vga.PutChar('\n'); Prompt.Write(); continue; }

            // Serial console first: this is how a headless session
            // (-display none -serial stdio) drives the shell.
            int ch = Serial.Pop();
            if (ch >= 0) { FeedChar((char)ch, line, ref len); continue; }

            // Then the IRQ1 scancode ring. When both are empty, sleep: the PIT
            // handler preempts us and schedules the other tasks.
            int sc = Keyboard.Pop();
            if (sc < 0) { NativeMethods.halt(); continue; }

            if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
            if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
            if ((sc & 0x80) != 0) continue; // ignore other break codes

            if (sc == 0x1C) { FeedChar('\r', line, ref len); continue; } // Enter
            if (sc == 0x0E) { FeedChar('\b', line, ref len); continue; } // Backspace

            char c = Keyboard.MapScancode((byte)sc, shift);
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
            Vga.PutChar('\n');
            line[len] = '\0';
            Execute(line, len);
            len = 0;
            Prompt.Write();
            return;
        }
        if (c == '\b' || c == (char)0x7F)
        {
            if (len > 0)
            {
                len--;
                Vga.PutChar('\b');
                Vga.PutChar(' ');
                Vga.PutChar('\b');
            }
            return;
        }
        if (c >= ' ' && c < 127 && len < 127)
        {
            line[len++] = c;
            Vga.PutChar(c);
        }
    }

    // One command line: expand it, honour the trailing '&' (background), then
    // hand it to the first command that matches.
    private static void Execute(char* line, int len)
    {
        if (len == 0) return;

        // Expand $VAR references ($USER, $HOME, ...) before dispatch.
        // Undefined names round-trip, so SET PROMPT=$P$G keeps its $ tokens.
        char* expanded = stackalloc char[256];
        int elen = Environment.Expand(line, len, expanded, 256);
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

        if (ShellText.Eq(line, len, "VER")) { SystemCommands.Version(); return; }
        if (ShellText.Eq(line, len, "ABOUT")) { SystemCommands.About(); return; }
        if (ShellText.Eq(line, len, "HELP")) { SystemCommands.Help(); return; }
        if (ShellText.Eq(line, len, "CLS")) { SystemCommands.Clear(); return; }
        if (ShellText.Eq(line, len, "MEM")) { SystemCommands.Memory(); return; }
        if (ShellText.Eq(line, len, "HALT")) { SystemCommands.Halt(); return; }
        if (ShellText.Eq(line, len, "POWEROFF") || ShellText.Eq(line, len, "SHUTDOWN") ||
            ShellText.Eq(line, len, "EXIT") || ShellText.Eq(line, len, "QUIT"))
        {
            SystemCommands.PowerOff();
            return;
        }
        if (ShellText.Eq(line, len, "REBOOT")) { SystemCommands.Reboot(); return; }
        if (ShellText.Eq(line, len, "TIME")) { SystemCommands.Time(); return; }
        if (ShellText.Eq(line, len, "DATE")) { SystemCommands.Date(); return; }
        if (ShellText.Eq(line, len, "TICKS")) { SystemCommands.Ticks(); return; }
        if (ShellText.StartsWith(line, len, "SLEEP")) { SystemCommands.Sleep(line, len); return; }
        if (ShellText.StartsWith(line, len, "ECHO")) { SystemCommands.Echo(line, len); return; }

        if (ShellText.Eq(line, len, "SET") || ShellText.StartsWith(line, len, "SET "))
        {
            EnvironmentCommands.Set(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "DIR") || ShellText.StartsWith(line, len, "DIR "))
        {
            FileCommands.Dir(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "CD") || ShellText.StartsWith(line, len, "CD "))
        {
            FileCommands.Cd(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "TYPE") || ShellText.StartsWith(line, len, "TYPE "))
        {
            FileCommands.Type(line, len);
            return;
        }

        if (ShellText.Eq(line, len, "DEMO")) { TaskCommands.Demo(background); return; }
        if (ShellText.Eq(line, len, "TASK") || ShellText.Eq(line, len, "PS")) { TaskCommands.List(); return; }
        if (ShellText.Eq(line, len, "KILL") || ShellText.StartsWith(line, len, "KILL ")) { TaskCommands.Kill(line, len); return; }
        if (ShellText.Eq(line, len, "WAIT") || ShellText.StartsWith(line, len, "WAIT ")) { TaskCommands.Wait(line, len); return; }
        if (ShellText.Eq(line, len, "TAB") || ShellText.StartsWith(line, len, "TAB ")) { TaskCommands.Tab(line, len); return; }
        if (ShellText.Eq(line, len, "SPAWN") || ShellText.StartsWith(line, len, "SPAWN ") ||
            ShellText.Eq(line, len, "RING3") || ShellText.StartsWith(line, len, "RING3 "))
        {
            int i = 5;
            while (i < len && line[i] == ' ') i++;
            int prog = Layout.ProgHello;
            if (ShellText.StartsWithAt(line, len, i, "SPAWNER")) prog = Layout.ProgSpawner;
            else if (ShellText.StartsWithAt(line, len, i, "BAD")) prog = Layout.ProgBad;
            TaskCommands.Spawn(prog, background);
            return;
        }

        Vga.WriteLine("Bad command or file name.");
    }
}