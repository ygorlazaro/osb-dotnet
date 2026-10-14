// The OSB shell: one prompt, one input line, one command at a time.
//
// Input arrives from two places and is folded into the same editing path: the
// COM1 serial console (IRQ4, how a headless -serial stdio session drives the
// shell) and the PS/2 keyboard (IRQ1). When both rings are empty the shell
// halts; the PIT handler preempts it and runs the other tasks.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Shell.Commands;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell;

internal static unsafe class Shell
{
    public static void Run()
    {
        Ext4.Mount();
        Osb.Kernel.Shell.Commands.FileCommands.LoadColor();
        Prompt.Write(); Vga.PutChar('\n');
        char* line = stackalloc char[128];
        int len = 0;
        bool shift = false;
        bool extended = false;   // 0xE0 prefix of the extended PS/2 keys
        int esc = 0;             // serial arrow-key state machine:
                                 // 0 = normal, 1 = after ESC, 2 = after ESC[/O

        while (true)
        {
            if (ConsoleControl.TakeCtrlC())
            {
                len = 0;
                History.NavReset();
                Vga.PutChar('\n');
                Prompt.Write();
                continue;
            }

            // Serial console first: this is how a headless session
            // (-display none -serial stdio) drives the shell.
            int ch = Serial.Pop();
            if (ch >= 0)
            {
                // Arrow keys arrive as 3-byte sequences: ESC [ A/B (normal) or
                // ESC O A/B (application cursor mode, what xterm/vt100 send).
                // The state machine consumes one byte per iteration, so a byte
                // that has not arrived yet is never dropped mid-sequence.
                if (esc == 1)
                {
                    esc = 0;
                    if (ch == '[' || ch == 'O') { esc = 2; continue; }
                    FeedChar((char)ch, line, ref len);   // lone ESC: reprocess
                    continue;
                }
                if (esc == 2)
                {
                    esc = 0;
                    if (ch == 'A') { HistoryNav(line, ref len, true); continue; }
                    if (ch == 'B') { HistoryNav(line, ref len, false); continue; }
                    FeedChar((char)ch, line, ref len);   // unknown: echo it
                    continue;
                }
                if (ch == 27) { esc = 1; continue; }
                FeedChar((char)ch, line, ref len);
                continue;
            }

            // Then the IRQ1 scancode ring. When both are empty, sleep: the PIT
            // handler preempts us and schedules the other tasks.
            int sc = Keyboard.Pop();
            if (sc < 0) { NativeMethods.halt(); continue; }

            if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
            if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
            if (sc == 0xE0) { extended = true; continue; }   // extended-key prefix

            if (extended)
            {
                extended = false;
                if (sc == 0x48) { HistoryNav(line, ref len, true); continue; }   // Up
                if (sc == 0x50) { HistoryNav(line, ref len, false); continue; }  // Down
                continue;   // other extended keys: not used by the shell
            }

            if ((sc & 0x80) != 0) continue; // ignore other break codes

            if (sc == 0x1C) { FeedChar('\r', line, ref len); continue; } // Enter
            if (sc == 0x0E) { FeedChar('\b', line, ref len); continue; } // Backspace
            if (sc == 0x0F) { FeedChar('\t', line, ref len); continue; } // Tab

            char c = Keyboard.MapScancode((byte)sc, shift);
            if (c != 0) FeedChar(c, line, ref len);
        }
    }

    // Up/Down through the history, redrawing the edit line on screen.
    private static void HistoryNav(char* line, ref int len, bool up)
    {
        int old = len;
        bool ok = up ? History.Prev(line, ref len) : History.Next(line, ref len);
        if (ok) RedrawLine(line, len, old);
    }

    // Erases what is on screen and prints the (new) line contents.
    private static void RedrawLine(char* line, int len, int oldLen)
    {
        for (int i = 0; i < oldLen; i++)
        {
            Vga.PutChar('\b');
            Vga.PutChar(' ');
            Vga.PutChar('\b');
        }
        Vga.WriteStr(line, len);
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
            History.NavReset();
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
        if (c == '\t')
        {
            // complete commands / flags / filenames in place
            Completion.Complete(line, ref len);
            return;
        }
        if (c >= ' ' && c < 127 && len < 127)
        {
            line[len++] = c;
            Vga.PutChar(c);
        }
    }

    // One command line: recall "(:N)" when that is the syntax, record it in the
    // history, expand $VARs, honour the trailing '&' (background), then hand it
    // to the first command that matches.
    private static void Execute(char* line, int len)
    {
        if (len == 0) return;

        // "(:N)" — recall and re-run the Nth history entry.
        if (History.LooksLikeRecall(line, len))
        {
            char* rc = stackalloc char[History.LineCap];
            int rlen;
            if (History.ParseRecall(line, len, rc, out rlen))
            {
                for (int k = 0; k < rlen; k++) line[k] = rc[k];
                line[rlen] = '\0';
                len = rlen;
                Vga.WriteStr(line, len);   // echo what is being re-run
                Vga.PutChar('\n');
                History.Push(line, len);
            }
            else
            {
                Vga.WriteLine("No such history entry.");
                return;                     // a broken recall is not recorded
            }
        }
        else
        {
            History.Push(line, len);
        }

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
        if (ShellText.Eq(line, len, "PWD"))
        {
            FileCommands.Pwd(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "CLEAR"))
        {
            SystemCommands.Clear();
            return;
        }
        if (ShellText.Eq(line, len, "HISTORY") || ShellText.StartsWith(line, len, "HISTORY "))
        {
            History.ListCommand(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "TYPE") || ShellText.StartsWith(line, len, "TYPE "))
        {
            FileCommands.Type(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "DEL") || ShellText.StartsWith(line, len, "DEL ") ||
            ShellText.Eq(line, len, "ERASE") || ShellText.StartsWith(line, len, "ERASE ") ||
            ShellText.Eq(line, len, "DELETE") || ShellText.StartsWith(line, len, "DELETE "))
        {
            FileCommands.Del(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "MD") || ShellText.StartsWith(line, len, "MD ") ||
            ShellText.Eq(line, len, "MKDIR") || ShellText.StartsWith(line, len, "MKDIR "))
        {
            FileCommands.MkDir(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "RD") || ShellText.StartsWith(line, len, "RD ") ||
            ShellText.Eq(line, len, "RMDIR") || ShellText.StartsWith(line, len, "RMDIR ") ||
            ShellText.Eq(line, len, "RDDR") || ShellText.StartsWith(line, len, "RDDR "))
        {
            FileCommands.RdDir(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "REN") || ShellText.StartsWith(line, len, "REN ") ||
            ShellText.Eq(line, len, "RENAME") || ShellText.StartsWith(line, len, "RENAME ") ||
            ShellText.Eq(line, len, "MOVE") || ShellText.StartsWith(line, len, "MOVE "))
        {
            FileCommands.Ren(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "COPY") || ShellText.StartsWith(line, len, "COPY ") ||
            ShellText.Eq(line, len, "CP") || ShellText.StartsWith(line, len, "CP "))
        {
            FileCommands.Copy(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "SIZE") || ShellText.StartsWith(line, len, "SIZE "))
        {
            FileCommands.Size(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "COLOR") || ShellText.StartsWith(line, len, "COLOR "))
        {
            FileCommands.Color(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "LN") || ShellText.StartsWith(line, len, "LN ") ||
            ShellText.Eq(line, len, "LNK") || ShellText.StartsWith(line, len, "LNK "))
        {
            FileCommands.Ln(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "OSLANG") || ShellText.StartsWith(line, len, "OSLANG "))
        {
            global::Kernel.OslangCmd(line, len);   // interpreter entry (OslangKernel.cs)
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