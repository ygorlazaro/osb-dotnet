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
    // Boot into a session: the login prompt runs until somebody gets in; the
    // input loop returns on EXIT/LOGOUT, which drops back to the prompt.
    public static void Run()
    {
        Users.Init();
        for (;;)
        {
            LoginSequence();          // returns only on a successful login
            Ext4.Mount();
            Osb.Kernel.Shell.Commands.FileCommands.LoadColor();
            Prompt.Write(); Vga.PutChar('\n');
            Session();                // the input loop; returns on EXIT/LOGOUT
            Users.Logout();
        }
    }

    // Interactive login: "login:" and, for password-protected accounts,
    // "Password:" (no echo). Loops until a login succeeds.
    public static void LoginSequence()
    {
        char* name = stackalloc char[32];
        char* pass = stackalloc char[32];
        while (true)
        {
            if (ConsoleControl.TakeCtrlC()) { Vga.PutChar('\n'); continue; }

            Vga.Write("login: ");
            int nlen = ReadLine(name, 32, echo: true);
            if (nlen <= 0) continue;

            int idx = Users.Find(name, nlen);
            if (idx < 0) { Vga.WriteLine("Login incorrect"); continue; }

            if (Users.PassLen(idx) > 0)
            {
                Vga.Write("Password: ");
                int plen = ReadLine(pass, 32, echo: false);
                Vga.PutChar('\n');
                if (!Users.PassMatches(idx, pass, plen))
                {
                    Vga.WriteLine("Login incorrect");
                    continue;
                }
            }
            SessionSetup(idx);
            Vga.PutChar('\n');
            return;
        }
    }

    // Applies a successful login: session identity, $USER/$HOME, and the home
    // directory as the current one (A:\OSB when the folder is gone).
    public static void SessionSetup(int idx)
    {
        Users.SetSession(idx);

        char* userVar = stackalloc char[8];
        fixed (char* p = "USER") { for (int k = 0; k < 4; k++) userVar[k] = p[k]; }

        char* nm = stackalloc char[Users.NameCap];
        int nl = Users.NameAt(idx, nm);
        Environment.SetChars(userVar, 4, nm, nl);          // $USER

        char* home = stackalloc char[64];
        int hl = Users.HomeAt(idx, home);
        char* homeVar = stackalloc char[8];
        fixed (char* p = "HOME") { for (int k = 0; k < 4; k++) homeVar[k] = p[k]; }
        Environment.ForceSetChars(homeVar, 4, home, hl);   // $HOME (read-only)

        if (Ext4.Mount())
        {
            byte* canon = stackalloc byte[300];
            int clen = 0;
            uint ino;
            if (Ext4.ResolveCanonical(home, hl, out ino, canon, ref clen))
            {
                Cwd.Set(canon, clen);
                return;
            }
        }
        byte* root = stackalloc byte[4];
        root[0] = (byte)'A'; root[1] = (byte)':'; root[2] = (byte)'\\';
        Cwd.Set(root, 3);
    }

    // One input line from the serial console or the PS/2 keyboard. `echo`
    // prints what is typed (off for passwords). Returns the length.
    public static int ReadLine(char* buf, int cap, bool echo)
    {
        int len = 0;
        bool shift = false;
        while (true)
        {
            if (ConsoleControl.TakeCtrlC())
            {
                len = 0;
                Vga.PutChar('\n');
                continue;
            }

            int ch = Serial.Pop();
            if (ch < 0)
            {
                int sc = Keyboard.Pop();
                if (sc < 0) { NativeMethods.halt(); continue; }
                if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
                if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
                if ((sc & 0x80) != 0) continue;
                if (sc == 0x1C) ch = '\r';
                else if (sc == 0x0E) ch = '\b';
                else
                {
                    char c = Keyboard.MapScancode((byte)sc, shift);
                    if (c == 0) continue;
                    ch = c;
                }
            }

            if (ch == '\r' || ch == '\n') { Vga.PutChar('\n'); buf[len] = '\0'; return len; }
            if (ch == '\b' || ch == (char)0x7F)
            {
                if (len > 0)
                {
                    len--;
                    if (echo)
                    {
                        Vga.PutChar('\b');
                        Vga.PutChar(' ');
                        Vga.PutChar('\b');
                    }
                }
                continue;
            }
            if (ch >= ' ' && ch < 127 && len < cap - 1)
            {
                buf[len++] = (char)ch;
                if (echo) Vga.PutChar((char)ch);
            }
        }
    }

    // The console input loop: reads the line from serial/keyboard and runs it.
    // Returns on EXIT/LOGOUT (the login prompt takes over).
    private static void Session()
    {
        char* line = stackalloc char[128];
        int len = 0;
        bool shift = false;
        bool extended = false;   // 0xE0 prefix of the extended PS/2 keys
        int esc = 0;             // serial arrow-key state machine:
                                 // 0 = normal, 1 = after ESC, 2 = after ESC[/O

        while (true)
        {
            if (_sessionOver) { _sessionOver = false; return; }

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

    private static bool _sessionOver;

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
            if (!_sessionOver) Prompt.Write();
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

        // A '|' chains commands: each stage's console output feeds the
        // next stage's input (appended after the stage's own arguments).
        // Pipelines always run in the foreground (a trailing '&' is
        // ignored while piping).
        bool pipe = false;
        for (int k = 0; k < len; k++) if (line[k] == '|') { pipe = true; break; }
        if (pipe) { RunPipes(line, len); return; }

        Dispatch(line, len, background);
    }

    // ---- pipes ----
    // "A | B" runs A, captures its console output, and appends it to
    // B's command line, so commands like GREP can filter it. The
    // captured text lands after the first '\n' of the next stage's
    // line; commands that read piped input split there.

    private const int PipeCap = 4096;
    private unsafe struct PipeBuf { public fixed char data[PipeCap]; }
    private static PipeBuf _pipeStage;

    private static void RunPipes(char* line, int len)
    {
        fixed (char* stage = _pipeStage.data)
        {
            int pos = 0;
            bool first = true;
            while (pos <= len)
            {
                int s = pos;
                while (pos < len && line[pos] != '|') pos++;
                int sl = pos - s;
                while (sl > 0 && line[s + sl - 1] == ' ') sl--;
                while (sl > 0 && line[s] == ' ') { s++; sl--; }

                int kl = 0;
                for (int k = 0; k < sl; k++) stage[kl++] = line[s + k];
                if (!first)
                {
                    stage[kl++] = '\n';
                    byte* cap = Vga.CaptureBuffer;
                    int capLen = Vga.CaptureLength;
                    for (int k = 0; k < capLen && kl < PipeCap - 1; k++) stage[kl++] = (char)cap[k];
                }

                if (kl > 0)
                {
                    // Capture only when another stage follows: the last
                    // stage must print to the console, not the buffer.
                    bool more = pos < len;   // stopped at a '|', not at the end
                    if (more) Vga.BeginCapture();
                    Dispatch(stage, kl, false);
                    if (more) Vga.EndCapture();
                }

                first = false;
                if (pos >= len) break;
                pos++;   // skip '|'
            }
        }
    }

    // The command dispatch: first match wins. Public so SUDO can re-enter it
    // as root. `background` carries the trailing '&' parsed by Execute.
    public static void Dispatch(char* line, int len, bool background = false)
    {
        // COMMAND /? (or -?) - per-command help page. Unknown commands
        // fall through, so external programs receive the flag themselves.
        {
            int i = 0;
            while (i < len && line[i] == ' ') i++;
            int cs = i;
            while (i < len && line[i] != ' ') i++;
            int cl = i - cs;
            bool help = false;
            while (i < len)
            {
                while (i < len && line[i] == ' ') i++;
                if (i >= len) break;
                int s = i;
                while (i < len && line[i] != ' ') i++;
                if (i - s == 2 && (line[s] == '/' || line[s] == '-') && line[s + 1] == '?')
                {
                    help = true;
                    break;
                }
            }
            if (help && CommandHelp.TryShow(line + cs, cl)) return;
        }

        if (ShellText.Eq(line, len, "VER")) { SystemCommands.Version(); return; }
        if (ShellText.Eq(line, len, "ABOUT")) { SystemCommands.About(); return; }
        if (ShellText.Eq(line, len, "HELP") || ShellText.StartsWith(line, len, "HELP "))
        {
            SystemCommands.Help(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "CLS")) { SystemCommands.Clear(); return; }
        if (ShellText.Eq(line, len, "MEM")) { SystemCommands.Memory(); return; }
        if (ShellText.Eq(line, len, "HALT")) { SystemCommands.Halt(); return; }
        if (ShellText.Eq(line, len, "POWEROFF") || ShellText.Eq(line, len, "SHUTDOWN") ||
            ShellText.Eq(line, len, "QUIT"))
        {
            SystemCommands.PowerOff();
            return;
        }
        // EXIT/LOGOUT end the session: back to the login prompt
        if (ShellText.Eq(line, len, "EXIT") || ShellText.Eq(line, len, "LOGOUT"))
        {
            _sessionOver = true;
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
        if (ShellText.Eq(line, len, "WHOAMI"))
        {
            SystemCommands.WhoAmI();
            return;
        }
        if (ShellText.Eq(line, len, "USER") || ShellText.StartsWith(line, len, "USER "))
        {
            UserCommands.User(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "LOGIN") || ShellText.StartsWith(line, len, "LOGIN "))
        {
            UserCommands.Login(line, len);
            return;
        }
        if (ShellText.StartsWith(line, len, "CHOWN"))
        {
            UserCommands.Chown(line, len);
            return;
        }
        if (ShellText.StartsWith(line, len, "CHMOD"))
        {
            UserCommands.Chmod(line, len);
            return;
        }
        if (ShellText.Eq(line, len, "SUDO") || ShellText.StartsWith(line, len, "SUDO "))
        {
            UserCommands.Sudo(line, len);
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

        if (ShellText.Eq(line, len, "KISS") || ShellText.StartsWith(line, len, "KISS "))
        {
            KissCommand.Run(line, len);
            return;
        }

        if (ShellText.Eq(line, len, "TRASH") || ShellText.StartsWith(line, len, "TRASH "))
        {
            TrashCommands.Run(line, len);
            return;
        }

        if (ShellText.Eq(line, len, "GREP") || ShellText.StartsWith(line, len, "GREP "))
        {
            GrepCommand.Run(line, len);
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

        // not a built-in: try it as an external program in PATH (or .\...)
        if (TryExternal(line, len, background)) return;

        Vga.WriteLine("Bad command or file name.");
    }

    // Try the line as an external program: a ".\"-prefixed name runs from the
    // current directory; anything else is looked up in each PATH entry (the
    // name, then the name with ".EXE"). Returns true when a program started.
    private static bool TryExternal(char* line, int len, bool background)
    {
        // first token = program name, the rest is the argument string
        int s = 0;
        while (s < len && line[s] == ' ') s++;
        int e = s;
        while (e < len && line[e] != ' ') e++;
        int nameLen = e - s;
        if (nameLen <= 0) return false;
        char* name = line + s;

        int argS = e;
        while (argS < len && line[argS] == ' ') argS++;
        int argLen = len - argS;
        char* args = line + argS;
        if (argLen > 92) argLen = 92;

        char* full = stackalloc char[300];

        // .\prog or ./prog: relative to the current directory
        if (nameLen >= 2 && name[0] == '.' && (name[1] == '\\' || name[1] == '/'))
        {
            int fl = 0;
            byte* cwd = stackalloc byte[160];
            int cl = Cwd.CopyTo(cwd, 160);
            for (int k = 0; k < cl && fl < 290; k++) full[fl++] = (char)cwd[k];
            for (int k = 2; k < nameLen && fl < 290; k++) full[fl++] = name[k];
            if (fl == 0) return false;
            full[fl] = '\0';
            if (Storage.Ext4.Resolve(full, fl, out _))
            {
                JobControl.StartFile(full, fl, args, argLen, background);
                return true;
            }
            return false;
        }

        // PATH search: each ';'-separated entry, trying name and name.EXE
        int po, pl;
        fixed (char* ppath = "PATH") { if (!Environment.TryGet(ppath, 4, out po, out pl)) return false; }
        char* pathVar = stackalloc char[128];
        int pn = pl < 127 ? pl : 127;
        for (int k = 0; k < pn; k++) pathVar[k] = Environment.ValueAt(po + k);

        int i = 0;
        while (i < pn)
        {
            // one PATH entry
            int es = i;
            while (i < pn && pathVar[i] != ';') i++;
            int el = i - es;
            i++;   // skip the ';'
            if (el <= 0) continue;

            // <entry>\<name>  and  <entry>\<name>.EXE
            if (BuildExePath(full, pathVar + es, el, name, nameLen, false) &&
                Storage.Ext4.Resolve(full, StrLen(full), out _))
            {
                JobControl.StartFile(full, StrLen(full), args, argLen, background);
                return true;
            }
            if (BuildExePath(full, pathVar + es, el, name, nameLen, true) &&
                Storage.Ext4.Resolve(full, StrLen(full), out _))
            {
                JobControl.StartFile(full, StrLen(full), args, argLen, background);
                return true;
            }
        }
        return false;
    }

    // "<entry>\<name>" (or with ".EXE" appended), NUL-terminated in `dst`.
    private static bool BuildExePath(char* dst, char* entry, int entryLen, char* name, int nameLen, bool exe)
    {
        int n = 0;
        for (int k = 0; k < entryLen && n < 260; k++) dst[n++] = entry[k];
        if (n == 0 || dst[n - 1] != '\\') dst[n++] = '\\';
        for (int k = 0; k < nameLen && n < 260; k++) dst[n++] = name[k];
        if (exe && n < 256)
        {
            dst[n++] = '.';
            dst[n++] = 'E';
            dst[n++] = 'X';
            dst[n++] = 'E';
        }
        if (n >= 259) return false;
        dst[n] = '\0';
        return true;
    }

    private static int StrLen(char* s)
    {
        int n = 0;
        while (s[n] != '\0') n++;
        return n;
    }
}