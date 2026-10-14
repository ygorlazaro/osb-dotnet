// Per-command help pages: COMMAND /? (or COMMAND -?) prints the page
// of that command; HELP <command> shows the same page.
//
// The table is one const string: "NAME\nline\nline\n\0" per record,
// a trailing "\0\0" ends it. No managed arrays (the freestanding AOT
// runtime has no bounds-check helper), so the blob is walked through
// a fixed pointer, char by char, the way ShellText does.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell.Commands;

internal static class CommandHelp
{
    private const string Pages =
        "VER\n" +
        "  Prints the OSB version string.\n" +
        "  Usage: VER\n" +
        "\0" +
        "ABOUT\n" +
        "  Shows information about OSB and its creator.\n" +
        "  Usage: ABOUT\n" +
        "\0" +
        "HELP\n" +
        "  Without arguments: the command list. With a name: that\n" +
        "  command's help page.\n" +
        "  Usage: HELP [command]   (or: COMMAND /?)\n" +
        "  Pipes: A | B feeds A's output as B's input.\n" +
        "\0" +
        "CLS\n" +
        "  Clears the screen.\n" +
        "  Usage: CLS   (alias: CLEAR)\n" +
        "\0" +
        "CLEAR\n" +
        "  Clears the screen.\n" +
        "  Usage: CLEAR   (alias: CLS)\n" +
        "\0" +
        "MEM\n" +
        "  Reports physical memory usage of the frame allocator.\n" +
        "  Usage: MEM\n" +
        "\0" +
        "HALT\n" +
        "  Stops the CPU until the next interrupt.\n" +
        "  Usage: HALT\n" +
        "  In QEMU, exit the emulator with Ctrl+Alt+2, then 'quit'.\n" +
        "\0" +
        "POWEROFF\n" +
        "  Powers off the machine (ACPI S5).\n" +
        "  Usage: POWEROFF   (aliases: SHUTDOWN, QUIT)\n" +
        "\0" +
        "SHUTDOWN\n" +
        "  Powers off the machine (ACPI S5).\n" +
        "  Usage: SHUTDOWN   (alias of POWEROFF)\n" +
        "\0" +
        "QUIT\n" +
        "  Powers off the machine (ACPI S5).\n" +
        "  Usage: QUIT   (alias of POWEROFF)\n" +
        "\0" +
        "EXIT\n" +
        "  Ends the session and returns to the login prompt.\n" +
        "  Usage: EXIT   (alias: LOGOUT)\n" +
        "\0" +
        "LOGOUT\n" +
        "  Ends the session and returns to the login prompt.\n" +
        "  Usage: LOGOUT   (alias: EXIT)\n" +
        "\0" +
        "REBOOT\n" +
        "  Reboots the machine (8042 reset; never returns).\n" +
        "  Usage: REBOOT\n" +
        "\0" +
        "TIME\n" +
        "  Shows the current time from the RTC.\n" +
        "  Usage: TIME\n" +
        "\0" +
        "DATE\n" +
        "  Shows the current date from the RTC.\n" +
        "  Usage: DATE\n" +
        "\0" +
        "TICKS\n" +
        "  Shows system uptime in seconds and PIT ticks.\n" +
        "  Usage: TICKS\n" +
        "\0" +
        "SLEEP\n" +
        "  Pauses the shell for N seconds (keys typed are queued).\n" +
        "  Usage: SLEEP <seconds>\n" +
        "\0" +
        "ECHO\n" +
        "  Prints a line of text.\n" +
        "  Usage: ECHO <text>\n" +
        "\0" +
        "SET\n" +
        "  Lists environment variables, or sets one ($VAR; names are\n" +
        "  case-insensitive).\n" +
        "  Usage: SET [NAME=value]\n" +
        "  SET NAME lists one variable; SET NAME= clears it.\n" +
        "  $HOME is read-only (the kernel sets it at login).\n" +
        "\0" +
        "DIR\n" +
        "  Lists files and directories (ext4, drive A:).\n" +
        "  Usage: DIR [path] [/W]\n" +
        "  /W lists one name per line.\n" +
        "\0" +
        "CD\n" +
        "  Changes the current directory of the drive.\n" +
        "  Usage: CD [path]\n" +
        "  CD\\ returns to the root of A:.\n" +
        "\0" +
        "PWD\n" +
        "  Prints the current directory.\n" +
        "  Usage: PWD\n" +
        "\0" +
        "WHOAMI\n" +
        "  Prints the logged-in user name.\n" +
        "  Usage: WHOAMI\n" +
        "\0" +
        "USER\n" +
        "  Manages user accounts (uid 0 = root).\n" +
        "  Usage: USER [flag]\n" +
        "  /ADD <name> [password]   /CHANGE <name> [password]\n" +
        "  /DEL <name> [/HOME]      /SUDO <name>  |  /UNSUDO <name>\n" +
        "\0" +
        "LOGIN\n" +
        "  Switches the session user (root is passwordless by default).\n" +
        "  Usage: LOGIN [name]\n" +
        "\0" +
        "CHOWN\n" +
        "  Changes the owner of a file or directory.\n" +
        "  Usage: CHOWN <user> <path>   (root or current owner)\n" +
        "\0" +
        "CHMOD\n" +
        "  Changes the permission bits of a file or directory.\n" +
        "  Usage: CHMOD <octal> <path>   (root or current owner)\n" +
        "  Example: CHMOD 644 A:\\OSB\\README.TXT\n" +
        "\0" +
        "SUDO\n" +
        "  Runs a command as root (asks for the user's password).\n" +
        "  Usage: SUDO <command>\n" +
        "  The user must be listed in SUDOERS (A:\\OSB\\CONF).\n" +
        "\0" +
        "HISTORY\n" +
        "  Lists the command history, newest last.\n" +
        "  Usage: HISTORY [n]   (n = entries to show, 100 by default)\n" +
        "  (:N) re-runs entry N; Up/Down walk the history while editing.\n" +
        "\0" +
        "TYPE\n" +
        "  Prints the content of a text file.\n" +
        "  Usage: TYPE <file>\n" +
        "\0" +
        "DEL\n" +
        "  Moves a file to the per-user trash ($HOME\\TRASH).\n" +
        "  Usage: DEL <file>   (aliases: ERASE, DELETE)\n" +
        "  Undelete with: TRASH /U <name>\n" +
        "\0" +
        "ERASE\n" +
        "  Moves a file to the per-user trash ($HOME\\TRASH).\n" +
        "  Usage: ERASE <file>   (alias of DEL)\n" +
        "\0" +
        "DELETE\n" +
        "  Moves a file to the per-user trash ($HOME\\TRASH).\n" +
        "  Usage: DELETE <file>   (alias of DEL)\n" +
        "\0" +
        "MD\n" +
        "  Creates a directory.\n" +
        "  Usage: MD <directory>   (alias: MKDIR)\n" +
        "\0" +
        "MKDIR\n" +
        "  Creates a directory.\n" +
        "  Usage: MKDIR <directory>   (alias of MD)\n" +
        "\0" +
        "RD\n" +
        "  Removes an empty directory.\n" +
        "  Usage: RD <directory>   (aliases: RMDIR, RDDR)\n" +
        "\0" +
        "RMDIR\n" +
        "  Removes an empty directory.\n" +
        "  Usage: RMDIR <directory>   (alias of RD)\n" +
        "\0" +
        "RDDR\n" +
        "  Removes an empty directory.\n" +
        "  Usage: RDDR <directory>   (alias of RD)\n" +
        "\0" +
        "REN\n" +
        "  Renames a file (inside the same directory).\n" +
        "  Usage: REN <old> <new>   (alias: RENAME)\n" +
        "\0" +
        "RENAME\n" +
        "  Renames a file (inside the same directory).\n" +
        "  Usage: RENAME <old> <new>   (alias of REN)\n" +
        "\0" +
        "MOVE\n" +
        "  Moves a file within the same directory.\n" +
        "  Usage: MOVE <old> <new>\n" +
        "\0" +
        "COPY\n" +
        "  Copies a file (single file only; no wildcards).\n" +
        "  Usage: COPY <source> <destination>   (alias: CP)\n" +
        "\0" +
        "CP\n" +
        "  Copies a file (single file only; no wildcards).\n" +
        "  Usage: CP <source> <destination>   (alias of COPY)\n" +
        "\0" +
        "SIZE\n" +
        "  Prints the size of a file or directory in bytes.\n" +
        "  Usage: SIZE [file]\n" +
        "\0" +
        "COLOR\n" +
        "  Sets the text colors (VGA 16-color palette).\n" +
        "  Usage: COLOR [fg] [bg]\n" +
        "  Both values 0-15; COLOR alone prints the color table.\n" +
        "\0" +
        "LN\n" +
        "  Links files: LN makes a hard link, LN /S a symbolic link.\n" +
        "  Usage: LN [/S] <source> <destination>   (alias: LNK)\n" +
        "\0" +
        "LNK\n" +
        "  Links files: LNK makes a hard link, LNK /S a symbolic link.\n" +
        "  Usage: LNK [/S] <source> <destination>   (alias of LN)\n" +
        "\0" +
        "OSLANG\n" +
        "  Runs an OSLANG script (0.72).\n" +
        "  Usage: OSLANG <script.OSL> [args]\n" +
        "  Looks in A:\\OSB\\BIN, then A:\\OSB\\APPS, then treats the\n" +
        "  word as a path on A:.\n" +
        "\0" +
        "KISS\n" +
        "  Runs the KISS text editor (an OSLANG application).\n" +
        "  Usage: KISS [file]\n" +
        "\0" +
        "TRASH\n" +
        "  Manages the per-user trash ($HOME\\TRASH, on the ext4 volume).\n" +
        "  Usage: TRASH [/F <pattern>]\n" +
        "  /F searches file names inside the trash (case-insensitive).\n" +
        "  TRASH /U <name> undeletes a file back to its original folder.\n" +
        "\0" +
        "GREP\n" +
        "  Prints the lines that contain a pattern (a line filter).\n" +
        "  Usage: GREP [/I] <pattern> [file]\n" +
        "  /I matches case-insensitively (default is case-sensitive).\n" +
        "  Without [file], filters the piped input: DIR | GREP txt\n" +
        "\0" +
        "DEMO\n" +
        "  Starts a demo (kernel) task.\n" +
        "  Usage: DEMO [&]   (& = background)\n" +
        "\0" +
        "TASK\n" +
        "  Lists processes: pid, name, state, memory, role.\n" +
        "  Usage: TASK   (alias: PS)\n" +
        "\0" +
        "PS\n" +
        "  Lists processes: pid, name, state, memory, role.\n" +
        "  Usage: PS   (alias of TASK)\n" +
        "\0" +
        "KILL\n" +
        "  Terminates a process.\n" +
        "  Usage: KILL <pid>   (the console itself cannot be killed)\n" +
        "\0" +
        "WAIT\n" +
        "  Waits for a child process and collects its exit code.\n" +
        "  Usage: WAIT <pid>\n" +
        "\0" +
        "TAB\n" +
        "  Brings a running process to the foreground.\n" +
        "  Usage: TAB <pid>\n" +
        "\0" +
        "SPAWN\n" +
        "  Starts a ring-3 process.\n" +
        "  Usage: SPAWN [BAD|SPAWNER] [&]   (alias: RING3)\n" +
        "\0" +
        "RING3\n" +
        "  Starts a ring-3 process.\n" +
        "  Usage: RING3 [BAD|SPAWNER] [&]   (alias of SPAWN)\n" +
        "\0" +
        "\0";

    // True when the command is known and its page was printed.
    public static unsafe bool TryShow(char* name, int nlen)
    {
        fixed (char* blob = Pages)
        {
            char* rec = blob;
            while (true)
            {
                bool same = true;
                for (int k = 0; k < nlen; k++)
                {
                    char c = name[k];
                    if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                    if (rec[k] != c) { same = false; break; }
                }
                if (same && rec[nlen] == '\n')
                {
                    char* lp = rec + nlen + 1;
                    while (*lp != '\0')
                    {
                        while (*lp != '\n' && *lp != '\0') { Vga.PutChar(*lp); lp++; }
                        Vga.PutChar('\n');
                        if (*lp == '\n') lp++;
                    }
                    return true;
                }
                while (*rec != '\0') rec++;
                rec++;
                if (*rec == '\0') return false;
            }
        }
    }
}
