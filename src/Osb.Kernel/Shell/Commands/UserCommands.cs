// User/session commands: USER /ADD /CHANGE /DEL /SUDO /UNSUDO, LOGIN, CHOWN,
// CHMOD, SUDO.
//
// Root (uid 0) manages accounts; CHMOD is for the owner or root; CHOWN is
// root-only, like unix. Everything persists through the Users subsystem
// (A:\OSB\CONF\PASSWD and A:\OSB\CONF\SUDOERS).

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class UserCommands
{
    // USER | USER /ADD name [password] | USER /CHANGE name [password] |
    // USER /DEL name [/HOME] | USER /SUDO name | USER /UNSUDO name
    public static unsafe void User(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // skip USER

        while (i < len && line[i] == ' ') i++;
        int fs = i;
        while (i < len && line[i] != ' ') i++;
        int fl = i - fs;

        if (fl == 0 || line[fs] != '/')
        {
            List();
            return;
        }

        char* flag = stackalloc char[16];
        int fn = fl < 15 ? fl : 15;
        for (int k = 0; k < fn; k++) flag[k] = ShellText.Up(line[fs + k]);

        while (i < len && line[i] == ' ') i++;
        int argS = i;
        while (i < len && line[i] != ' ') i++;
        int argL = i - argS;

        if (LitEq(flag, fn, "/ADD")) { Add(line, i, len, argS, argL); return; }
        if (LitEq(flag, fn, "/CHANGE")) { Change(line, i, len); return; }
        if (LitEq(flag, fn, "/DEL")) { Del(line, i, len, argS, argL); return; }
        if (LitEq(flag, fn, "/SUDO")) { SudoFlag(true, line, argS, argL); return; }
        if (LitEq(flag, fn, "/UNSUDO")) { SudoFlag(false, line, argS, argL); return; }

        Vga.WriteLine("Unknown USER flag. Use /ADD /CHANGE /DEL /SUDO /UNSUDO.");
    }

    private static void List()
    {
        char* nm = stackalloc char[Osb.Kernel.Shell.Users.NameCap];
        char* hm = stackalloc char[64];
        Vga.WriteLine("  NAME          UID   HOME                              FLAGS");
        for (int i = 0; i < Osb.Kernel.Shell.Users.Count; i++)
        {
            Vga.Write("  ");
            int nl = Osb.Kernel.Shell.Users.NameAt(i, nm);
            Vga.WriteStr(nm, nl);
            for (int k = nl; k < 14; k++) Vga.PutChar(' ');
            ShellText.WriteDecPad((ulong)Osb.Kernel.Shell.Users.UidAt(i), 5);
            Vga.Write("  ");
            int hl = Osb.Kernel.Shell.Users.HomeAt(i, hm);
            Vga.WriteStr(hm, hl);
            for (int k = hl; k < 34; k++) Vga.PutChar(' ');
            Vga.Write(Osb.Kernel.Shell.Users.IsSudoerAt(i) ? "sudo" : "-");
            if (i == Osb.Kernel.Shell.Users.CurrentIndex) Vga.Write(" *");
            Vga.PutChar('\n');
        }
    }

    private static unsafe void Add(char* line, int at, int len, int argS, int argL)
    {
        if (!Osb.Kernel.Shell.Users.IsRoot) { Vga.WriteLine("Permission denied"); return; }
        if (argL <= 0) { Vga.WriteLine("Usage: USER /ADD <name> [password]"); return; }

        char* name = line + argS;
        if (!Osb.Kernel.Shell.Users.ValidName(name, argL))
        {
            Vga.WriteLine("Invalid name (letters, digits, '_' and '-', up to 15 chars).");
            return;
        }
        if (Osb.Kernel.Shell.Users.Find(name, argL) >= 0)
        {
            Vga.WriteLine("User already exists");
            return;
        }

        // optional password argument
        while (at < len && line[at] == ' ') at++;
        int ps = at;
        while (at < len && line[at] != ' ') at++;
        char* pass = line + ps;
        int plen = at - ps;

        int nextUid = NextUid();
        if (!Osb.Kernel.Shell.Users.Add(name, argL, pass, plen))
        {
            Vga.WriteLine("Cannot add user");
            return;
        }

        // home folder: A:\OSB\HOME\<name>, owned by the new user (mode 0755)
        char* home = stackalloc char[64];
        fixed (char* p = "A:\\OSB\\HOME\\")
        {
            int hl = 0;
            for (int k = 0; k < 12; k++) home[hl++] = p[k];
            for (int k = 0; k < argL; k++) home[hl++] = name[k];
            if (!FileCommands.MakeDir(home, hl, (ushort)(0x4000 | 0x1ED), (uint)nextUid, out _))
            {
                Vga.WriteLine("User added, but the home folder could not be created");
                return;
            }

            // per-user trash: $HOME\TRASH (created by every boot too)
            char* trash = stackalloc char[80];
            int tl = 0;
            for (int k = 0; k < hl; k++) trash[tl++] = home[k];
            fixed (char* tp = "\\TRASH") { for (int k = 0; k < 6; k++) trash[tl++] = tp[k]; }
            FileCommands.MakeDir(trash, tl, (ushort)(0x4000 | 0x1ED), (uint)nextUid, out _);
        }
        Vga.WriteLine("OK");
    }

    private static int NextUid()
    {
        int max = 0;
        for (int i = 0; i < Osb.Kernel.Shell.Users.Count; i++)
        {
            int u = Osb.Kernel.Shell.Users.UidAt(i);
            if (u >= max) max = u + 1;
        }
        return max;
    }

    private static unsafe void Change(char* line, int at, int len)
    {
        // re-parse: USER /CHANGE <name> [password]
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // USER
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // /CHANGE
        while (i < len && line[i] == ' ') i++;
        int ns = i;
        while (i < len && line[i] != ' ') i++;
        int nl = i - ns;
        if (nl <= 0) { Vga.WriteLine("Usage: USER /CHANGE <name> [password]"); return; }

        char* name = line + ns;
        int idx = Osb.Kernel.Shell.Users.Find(name, nl);
        if (idx < 0) { Vga.WriteLine("No such user"); return; }

        // root changes anyone; a user changes only its own password
        if (!Osb.Kernel.Shell.Users.IsRoot && idx != Osb.Kernel.Shell.Users.CurrentIndex)
        {
            Vga.WriteLine("Permission denied");
            return;
        }

        char* pass = stackalloc char[32];
        int plen = 0;
        while (i < len && line[i] == ' ') i++;
        int ps = i;
        while (i < len && line[i] != ' ') i++;
        for (int k = ps; k < i && plen < 31; k++) pass[plen++] = line[k];

        if (plen == 0)
        {
            // no password on the command line: ask once (empty = passwordless)
            Vga.Write("New password: ");
            plen = Shell.ReadLine(pass, 32, echo: false);
            Vga.PutChar('\n');
        }

        if (Osb.Kernel.Shell.Users.ChangePassword(idx, pass, plen))
            Vga.WriteLine("Password changed");
        else
            Vga.WriteLine("Cannot change password");
    }

    private static unsafe void Del(char* line, int at, int len, int argS, int argL)
    {
        if (!Osb.Kernel.Shell.Users.IsRoot) { Vga.WriteLine("Permission denied"); return; }
        if (argL <= 0) { Vga.WriteLine("Usage: USER /DEL <name> [/HOME]"); return; }
        char* name = line + argS;
        int idx = Osb.Kernel.Shell.Users.Find(name, argL);
        if (idx < 0) { Vga.WriteLine("No such user"); return; }
        if (Osb.Kernel.Shell.Users.UidAt(idx) == 0) { Vga.WriteLine("Cannot delete root"); return; }

        // /HOME may appear anywhere after the name
        bool delHome = false;
        int i = at;
        while (i < len)
        {
            while (i < len && line[i] == ' ') i++;
            int s = i;
            while (i < len && line[i] != ' ') i++;
            int l = i - s;
            if (l == 5 && ShellText.StartsWithAt(line, len, s, "/HOME")) delHome = true;
        }

        if (delHome)
        {
            char* home = stackalloc char[64];
            int hl = Osb.Kernel.Shell.Users.HomeAt(idx, home);
            if (hl > 0 && !FileCommands.RemoveDir(home, hl))
            {
                // keep the account when the folder could not go
                Vga.WriteLine("Home not removed (not empty or missing)");
                return;
            }
        }

        if (Osb.Kernel.Shell.Users.Del(idx))
            Vga.WriteLine("OK");
        else
            Vga.WriteLine("Error");
    }

    private static unsafe void SudoFlag(bool on, char* line, int argS, int argL)
    {
        if (!Osb.Kernel.Shell.Users.IsRoot) { Vga.WriteLine("Permission denied"); return; }
        if (argL <= 0)
        {
            Vga.WriteLine(on ? "Usage: USER /SUDO <name>" : "Usage: USER /UNSUDO <name>");
            return;
        }
        char* name = line + argS;
        int idx = Osb.Kernel.Shell.Users.Find(name, argL);
        if (idx < 0) { Vga.WriteLine("No such user"); return; }
        if (on) Osb.Kernel.Shell.Users.AddSudoer(idx);
        else Osb.Kernel.Shell.Users.DelSudoer(idx);
        Vga.WriteLine("OK");
    }

    // ---- LOGIN ----

    // LOGIN [name [password]] — without arguments runs the interactive prompt.
    public static unsafe void Login(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // skip LOGIN
        while (i < len && line[i] == ' ') i++;
        int ns = i;
        while (i < len && line[i] != ' ') i++;
        int nl = i - ns;
        if (nl <= 0)
        {
            Shell.LoginSequence();
            return;
        }

        char* name = line + ns;
        int idx = Osb.Kernel.Shell.Users.Find(name, nl);
        if (idx < 0) { Vga.WriteLine("Login incorrect"); return; }

        char* pass = stackalloc char[32];
        int plen = 0;
        while (i < len && line[i] == ' ') i++;
        int ps = i;
        while (i < len && line[i] != ' ') i++;
        for (int k = ps; k < i && plen < 31; k++) pass[plen++] = line[k];

        if (Osb.Kernel.Shell.Users.PassLen(idx) > 0 && plen == 0)
        {
            Vga.Write("Password: ");
            plen = Shell.ReadLine(pass, 32, echo: false);
            Vga.PutChar('\n');
        }

        if (!Osb.Kernel.Shell.Users.PassMatches(idx, pass, plen))
        {
            Vga.WriteLine("Login incorrect");
            return;
        }
        Shell.SessionSetup(idx);
    }

    // ---- CHOWN / CHMOD ----

    // CHOWN <user> <path> — root only.
    public static unsafe void Chown(char* line, int len)
    {
        if (!Osb.Kernel.Shell.Users.IsRoot) { Vga.WriteLine("Permission denied"); return; }

        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // skip CHOWN
        while (i < len && line[i] == ' ') i++;
        int us = i;
        while (i < len && line[i] != ' ') i++;
        int ul = i - us;
        if (ul <= 0) { Vga.WriteLine("Usage: CHOWN <user> <path>"); return; }
        char* user = line + us;

        while (i < len && line[i] == ' ') i++;
        int ps = i;
        while (i < len && line[i] != ' ') i++;
        int pl = i - ps;
        if (pl <= 0) { Vga.WriteLine("Usage: CHOWN <user> <path>"); return; }
        char* path = line + ps;

        int idx = Osb.Kernel.Shell.Users.Find(user, ul);
        if (idx < 0) { Vga.WriteLine("No such user"); return; }

        if (Osb.Kernel.Shell.Users.Chown(path, pl, (uint)Osb.Kernel.Shell.Users.UidAt(idx)))
            Vga.WriteLine("OK");
        else
            Vga.WriteLine("Path not found");
    }

    // CHMOD <octal> <path> — owner or root.
    public static unsafe void Chmod(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // skip CHMOD
        while (i < len && line[i] == ' ') i++;
        int ms = i;
        while (i < len && line[i] != ' ') i++;
        int ml = i - ms;
        if (ml <= 0) { Vga.WriteLine("Usage: CHMOD <octal> <path>"); return; }

        // octal mode (3 or 4 digits)
        ushort bits = 0;
        for (int k = 0; k < ml; k++)
        {
            char c = line[ms + k];
            if (c < '0' || c > '7') { Vga.WriteLine("Invalid mode (octal digits)"); return; }
            bits = (ushort)((bits << 3) | (c - '0'));
        }

        while (i < len && line[i] == ' ') i++;
        int ps = i;
        while (i < len && line[i] != ' ') i++;
        int pl = i - ps;
        if (pl <= 0) { Vga.WriteLine("Usage: CHMOD <octal> <path>"); return; }
        char* path = line + ps;

        // only the owner (or root) may chmod
        if (!Osb.Kernel.Shell.Users.IsRoot)
        {
            uint ino;
            if (!Ext4.Resolve(path, pl, out ino)) { Vga.WriteLine("Path not found"); return; }
            byte* inode = stackalloc byte[256];
            if (!Ext4.ReadInode(ino, inode)) { Vga.WriteLine("Path not found"); return; }
            if ((uint)Osb.Kernel.Shell.Users.Uid != Ext4.InodeGetUid(inode))
            {
                Vga.WriteLine("Operation not permitted");
                return;
            }
        }

        if (Osb.Kernel.Shell.Users.ChmodPath(path, pl, bits))
            Vga.WriteLine("OK");
        else
            Vga.WriteLine("Path not found");
    }

    // ---- SUDO ----

    // SUDO <command...> — runs the command as root. Root runs it directly;
    // a sudoers user confirms its own password first.
    public static unsafe void Sudo(char* line, int len)
    {
        int i = 0;
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;   // skip SUDO
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Vga.WriteLine("Usage: SUDO <command>"); return; }
        char* cmd = line + i;
        int clen = len - i;

        if (Osb.Kernel.Shell.Users.IsRoot)
        {
            Shell.Dispatch(cmd, clen);
            return;
        }

        char* nm = stackalloc char[Osb.Kernel.Shell.Users.NameCap];
        int nl = Osb.Kernel.Shell.Users.NameAt(Osb.Kernel.Shell.Users.CurrentIndex, nm);
        if (!Osb.Kernel.Shell.Users.IsSudoer(nm, nl))
        {
            Vga.WriteLine("User is not in the sudoers file.");
            return;
        }

        char* pass = stackalloc char[32];
        Vga.Write("Password: ");
        int plen = Shell.ReadLine(pass, 32, echo: false);
        Vga.PutChar('\n');
        if (!Osb.Kernel.Shell.Users.PassMatches(Osb.Kernel.Shell.Users.CurrentIndex, pass, plen))
        {
            Vga.WriteLine("Sorry, try again.");
            return;
        }

        Osb.Kernel.Shell.Users.BeginSudo();
        Shell.Dispatch(cmd, clen);
        Osb.Kernel.Shell.Users.EndSudo();
    }

    // ---- helpers ----

    private static bool LitEq(char* s, int len, string lit)
    {
        if (len != lit.Length) return false;
        fixed (char* p = lit)
        {
            for (int i = 0; i < len; i++)
                if (s[i] != p[i]) return false;
        }
        return true;
    }
}
