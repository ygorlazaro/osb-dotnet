// Users, sessions and unix-like permissions for OSB.
//
// The user database lives in A:\OSB\CONF\PASSWD (one "name:password:uid:home"
// line per user; an empty password means passwordless, like the default root
// account) and the sudo list in A:\OSB\CONF\SUDOERS (one username per line).
// Numeric uids follow the unix convention: 0 is root and bypasses every
// permission check.
//
// Ownership is the ext4 inode uid field. The disk image creates everything
// under A:\OSB as uid 0 (root), so a path belongs to root unless CHOWN says
// otherwise; the exceptions are the per-user folders under A:\OSB\HOME (owned
// by their user, created by USER /ADD) and A:\OSB\TEMP (mode 0777, shared).
//
// Permissions are the classic rwx bits stored in the low 12 mode bits:
// root bypasses; otherwise the owner bits apply to the owner and the "other"
// bits to everyone else. CHMOD writes those bits, preserving the file type.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell;

internal static unsafe class Users
{
    public const int MaxUsers = 24;
    public const int NameCap = 16;
    private const int PassCap = 32;
    private const int HomeCap = 64;

    private unsafe struct UserTable
    {
        public fixed char names[MaxUsers * NameCap];
        public fixed char passes[MaxUsers * PassCap];
        public fixed char homes[MaxUsers * HomeCap];
        public fixed int uids[MaxUsers];
        public fixed int nameLens[MaxUsers];
        public fixed int passLens[MaxUsers];
        public fixed int homeLens[MaxUsers];
        public fixed byte sudoers[MaxUsers];
        public int count;
    }

    private static UserTable _users;
    private static int _current = -1;    // index of the logged-in user
    private static int _sudoSaved = -1;  // session index saved by BeginSudo
    private static bool _loaded;

    // ---- boot ----

    // Loads PASSWD/SUDOERS, seeding the defaults on a fresh image (root,
    // passwordless, sudoer) and making sure the shared TEMP is world-writable.
    public static void Init()
    {
        if (_loaded) return;
        if (!Ext4.Mount()) return;
        _loaded = true;

        if (!LoadPasswd())
        {
            // fresh image: root only, passwordless, home A:\OSB\ROOT
            _users.count = 0;
            AddEntry("root", "", 0, "A:\\OSB\\ROOT");
            _users.sudoers[0] = 1;
            SavePasswd();
        }
        else
        {
            MigrateRootHome();
        }
        LoadSudoers();

        // every user gets $HOME and $HOME\TRASH on the ext4 volume
        Osb.Kernel.Shell.Commands.TrashCommands.EnsureAll();

        // TEMP is the shared scratch area: world-writable, root-owned.
        char* temp = stackalloc char[16];
        int tl = 0;
        SetLit(temp, ref tl, "A:\\OSB\\TEMP");
        ChmodPath(temp, tl, (ushort)(0x4000 | 0x1FF));
    }

    // Older images seeded root's home as A:\OSB; the root home is
    // A:\OSB\ROOT now. Rewrites PASSWD when the old value is found.
    private static void MigrateRootHome()
    {
        for (int i = 0; i < _users.count; i++)
        {
            if (_users.uids[i] != 0) return;
            if (_users.homeLens[i] == 7)
            {
                fixed (char* op = "A:\\OSB")
                {
                    bool same = true;
                    for (int k = 0; k < 7; k++)
                        if (_users.homes[i * HomeCap + k] != op[k]) { same = false; break; }
                    if (same)
                    {
                        fixed (char* np = "A:\\OSB\\ROOT")
                        {
                            for (int k = 0; k < 11; k++) _users.homes[i * HomeCap + k] = np[k];
                        }
                        _users.homeLens[i] = 11;
                        SavePasswd();
                    }
                }
            }
            return;
        }
    }

    // ---- session ----

    // uid of the current user (-1 when nobody is logged in).
    public static int Uid => _current < 0 ? -1 : _users.uids[_current];

    public static bool IsRoot => Uid == 0;

    public static bool LoggedIn => _current >= 0;

    // Table index of the logged-in user (-1 when nobody is logged in).
    public static int CurrentIndex => _current;

    public static void SetSession(int idx) => _current = idx;

    public static void Logout() => _current = -1;

    // Runs the following commands as root (SUDO), restoring the session after.
    public static void BeginSudo()
    {
        _sudoSaved = _current;
        _current = 0;   // root is always the first entry
    }

    public static void EndSudo()
    {
        if (_sudoSaved >= 0) _current = _sudoSaved;
        _sudoSaved = -1;
    }

    // ---- permission checks (unix-ish: root bypasses, then owner, then other) ----

    public static bool CanRead(uint owner, ushort mode)
    {
        if (Uid == 0) return true;                        // root
        if ((uint)Uid == owner) return (mode & 0x100) != 0;   // owner r
        return (mode & 0x004) != 0;                       // other r
    }

    public static bool CanWrite(uint owner, ushort mode)
    {
        if (Uid == 0) return true;                        // root
        if ((uint)Uid == owner) return (mode & 0x080) != 0;   // owner w
        return (mode & 0x002) != 0;                       // other w
    }

    // ---- table access ----

    public static int Count => _users.count;

    // Case-insensitive lookup (the shell itself is case-insensitive).
    public static int Find(char* name, int len)
    {
        if (len <= 0) return -1;
        for (int i = 0; i < _users.count; i++)
        {
            if (_users.nameLens[i] != len) continue;
            bool same = true;
            for (int k = 0; k < len; k++)
            {
                char a = name[k];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                char b = _users.names[i * NameCap + k];
                if (b >= 'a' && b <= 'z') b -= (char)32;
                if (a != b) { same = false; break; }
            }
            if (same) return i;
        }
        return -1;
    }

    public static int UidAt(int idx) => _users.uids[idx];

    public static int PassLen(int idx) => _users.passLens[idx];

    public static bool PassMatches(int idx, char* pass, int passLen)
    {
        if (idx < 0) return false;
        if (_users.passLens[idx] != passLen) return false;
        for (int k = 0; k < passLen; k++)
            if (_users.passes[idx * PassCap + k] != pass[k]) return false;
        return true;
    }

    public static bool IsSudoerAt(int idx) => _users.sudoers[idx] != 0;

    public static bool IsSudoer(char* name, int len)
    {
        int idx = Find(name, len);
        return idx >= 0 && _users.sudoers[idx] != 0;
    }

    public static void SetSudoer(int idx, bool on) => _users.sudoers[idx] = (byte)(on ? 1 : 0);

    // Copies a field out by index (for listings and the session setup).
    public static int NameAt(int idx, char* dst)
    {
        int n = _users.nameLens[idx];
        for (int k = 0; k < n; k++) dst[k] = _users.names[idx * NameCap + k];
        return n;
    }

    public static int PassAt(int idx, char* dst)
    {
        int n = _users.passLens[idx];
        for (int k = 0; k < n; k++) dst[k] = _users.passes[idx * PassCap + k];
        return n;
    }

    public static int HomeAt(int idx, char* dst)
    {
        int n = _users.homeLens[idx];
        for (int k = 0; k < n; k++) dst[k] = _users.homes[idx * HomeCap + k];
        return n;
    }

    // uid -> username (for ls-style listings); false when the uid is unknown.
    public static bool NameOf(uint uid, char* dst, out int len)
    {
        for (int i = 0; i < _users.count; i++)
        {
            if ((uint)_users.uids[i] == uid)
            {
                len = NameAt(i, dst);
                return true;
            }
        }
        len = 0;
        return false;
    }

    // ---- mutations (all persist to PASSWD) ----

    // Validates a name: 1..15 chars of [A-Za-z0-9_-], no ':'.
    public static bool ValidName(char* name, int len)
    {
        if (len <= 0 || len >= NameCap) return false;
        for (int k = 0; k < len; k++)
        {
            char c = name[k];
            bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (!ok) return false;
        }
        return true;
    }

    // Adds a user (uid = max existing + 1, home A:\OSB\HOME\<name>).
    public static bool Add(char* name, int nlen, char* pass, int plen)
    {
        if (_users.count >= MaxUsers) return false;
        if (Find(name, nlen) >= 0) return false;      // already exists
        if (!ValidName(name, nlen)) return false;
        if (plen >= PassCap) plen = PassCap - 1;

        int uid = 0;
        for (int i = 0; i < _users.count; i++)
            if (_users.uids[i] >= uid) uid = _users.uids[i] + 1;

        char* home = stackalloc char[HomeCap];
        int hlen = 0;
        SetLit(home, ref hlen, "A:\\OSB\\HOME\\");
        for (int k = 0; k < nlen; k++) home[hlen++] = name[k];

        AddEntry(name, nlen, pass, plen, uid, home, hlen);
        SavePasswd();
        return true;
    }

    // Changes the password of user `idx` (empty = passwordless).
    public static bool ChangePassword(int idx, char* pass, int plen)
    {
        if (idx < 0 || plen >= PassCap) return false;
        int slot = idx * PassCap;
        for (int k = 0; k < plen; k++) _users.passes[slot + k] = pass[k];
        _users.passLens[idx] = plen;
        SavePasswd();
        return true;
    }

    // Removes user `idx` (the home folder is the caller's business).
    public static bool Del(int idx)
    {
        if (idx < 0 || idx >= _users.count) return false;
        for (int i = idx; i < _users.count - 1; i++)
        {
            CopyRow(i, i + 1);
        }
        _users.count--;
        SavePasswd();
        return true;
    }

    public static void AddSudoer(int idx)
    {
        if (idx < 0) return;
        _users.sudoers[idx] = 1;
        SaveSudoers();
    }

    public static void DelSudoer(int idx)
    {
        if (idx < 0) return;
        _users.sudoers[idx] = 0;
        SaveSudoers();
    }

    // ---- CHOWN / CHMOD on the filesystem ----

    public static unsafe bool Chown(char* path, int len, uint newUid)
    {
        if (!Ext4.Mount()) return false;
        uint ino;
        if (!Ext4.Resolve(path, len, out ino)) return false;
        byte* inode = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, inode)) return false;
        Ext4.Wr16(inode, Layout.InoUid, (ushort)newUid);
        Ext4.WriteInode(ino, inode);
        return true;
    }

    // Sets the low 12 permission bits, preserving the file-type bits.
    public static unsafe bool ChmodPath(char* path, int len, ushort bits)
    {
        if (!Ext4.Mount()) return false;
        uint ino;
        if (!Ext4.Resolve(path, len, out ino)) return false;
        byte* inode = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, inode)) return false;
        ushort mode = Ext4.InodeGetMode(inode);
        mode = (ushort)((mode & 0xF000) | (bits & 0x0FFF));
        Ext4.Wr16(inode, Layout.InoMode, mode);
        Ext4.WriteInode(ino, inode);
        return true;
    }

    // ---- PASSWD / SUDOERS persistence ----

    // Format: "name:password:uid:home" per line.
    private static bool LoadPasswd()
    {
        char* path = stackalloc char[32];
        int plen = 0;
        SetLit(path, ref plen, "A:\\OSB\\CONF\\PASSWD");

        uint ino;
        if (!Ext4.Resolve(path, plen, out ino)) return false;
        byte* buf = stackalloc byte[4096];
        int got = 0;
        if (!Ext4.ReadFileAll(ino, buf, 4096, out got) || got <= 0) return false;

        _users.count = 0;
        int pos = 0;
        while (pos < got && _users.count < MaxUsers)
        {
            // one line
            int eol = pos;
            while (eol < got && buf[eol] != '\n') eol++;
            ParsePasswdLine(buf + pos, eol - pos);
            pos = eol + 1;
        }
        // root must exist
        for (int i = 0; i < _users.count; i++)
            if (_users.uids[i] == 0) return true;
        return false;
    }

    private static void ParsePasswdLine(byte* line, int len)
    {
        // fields separated by ':' — name, password, uid, then home, which is
        // the LAST field and runs to the end of the line (paths carry their
        // own ':', as in "A:\OSB", so the split stops after the third colon).
        int* f = stackalloc int[4];     // field start offsets
        int* fl = stackalloc int[4];    // field lengths
        int nf = 0;
        int s = 0;
        for (int i = 0; i <= len && nf < 4; i++)
        {
            if (i == len || (line[i] == ':' && nf < 3))
            {
                f[nf] = s;
                fl[nf] = i - s;
                nf++;
                s = i + 1;
            }
        }
        if (nf < 3) return;
        // the home is whatever is left of the line
        if (nf == 3) { f[3] = s; fl[3] = len - s; nf = 4; }

        int idx = _users.count;
        int ns = fl[0] < NameCap ? fl[0] : NameCap - 1;
        for (int k = 0; k < ns; k++) _users.names[idx * NameCap + k] = (char)line[f[0] + k];
        _users.nameLens[idx] = ns;

        int ps = fl[1] < PassCap - 1 ? fl[1] : PassCap - 1;
        for (int k = 0; k < ps; k++) _users.passes[idx * PassCap + k] = (char)line[f[1] + k];
        _users.passLens[idx] = ps;

        int uid = 0;
        for (int k = 0; k < fl[2]; k++)
        {
            char c = (char)line[f[2] + k];
            if (c < '0' || c > '9') break;
            uid = uid * 10 + (c - '0');
        }
        _users.uids[idx] = uid;

        int hs = nf >= 4 ? (fl[3] < HomeCap - 1 ? fl[3] : HomeCap - 1) : 0;
        for (int k = 0; k < hs; k++) _users.homes[idx * HomeCap + k] = (char)line[f[3] + k];
        _users.homeLens[idx] = hs;

        _users.sudoers[idx] = 0;
        _users.count++;
    }

    private static void SavePasswd()
    {
        // build the file image
        byte* buf = stackalloc byte[4096];
        int n = 0;
        for (int i = 0; i < _users.count; i++)
        {
            for (int k = 0; k < _users.nameLens[i]; k++) buf[n++] = (byte)_users.names[i * NameCap + k];
            buf[n++] = (byte)':';
            for (int k = 0; k < _users.passLens[i]; k++) buf[n++] = (byte)_users.passes[i * PassCap + k];
            buf[n++] = (byte)':';
            n += DecToBuf(buf + n, (uint)_users.uids[i]);
            buf[n++] = (byte)':';
            for (int k = 0; k < _users.homeLens[i]; k++) buf[n++] = (byte)_users.homes[i * HomeCap + k];
            buf[n++] = (byte)'\n';
        }
        WriteConfFile("A:\\OSB\\CONF\\PASSWD", buf, n);
    }
    private static void LoadSudoers()
    {
        char* path = stackalloc char[32];
        int plen = 0;
        SetLit(path, ref plen, "A:\\OSB\\CONF\\SUDOERS");

        uint ino;
        if (!Ext4.Resolve(path, plen, out ino)) { SaveSudoers(); return; }
        byte* buf = stackalloc byte[1024];
        int got = 0;
        if (!Ext4.ReadFileAll(ino, buf, 1024, out got) || got <= 0) { SaveSudoers(); return; }

        int pos = 0;
        while (pos <= got)
        {
            int eol = pos;
            while (eol < got && buf[eol] != '\n') eol++;
            int len = eol - pos;
            if (len > 0)
            {
                // the file buffer is bytes: copy the line into a char buffer
                // (Find compares 16-bit chars; a byte* cast would pair bytes)
                char* nm = stackalloc char[64];
                int cn = len < 63 ? len : 63;
                for (int k = 0; k < cn; k++) nm[k] = (char)buf[pos + k];
                int idx = Find(nm, cn);
                if (idx >= 0) _users.sudoers[idx] = 1;
            }
            pos = eol + 1;
        }
    }

    private static void SaveSudoers()
    {
        byte* buf = stackalloc byte[1024];
        int n = 0;
        for (int i = 0; i < _users.count; i++)
        {
            if (_users.sudoers[i] == 0) continue;
            for (int k = 0; k < _users.nameLens[i]; k++) buf[n++] = (byte)_users.names[i * NameCap + k];
            buf[n++] = (byte)'\n';
        }
        WriteConfFile("A:\\OSB\\CONF\\SUDOERS", buf, n);
    }

    // Copies a path literal into a buffer and returns its true length.
    private static int ConfPath(char* dst, string lit)
    {
        fixed (char* p = lit)
        {
            for (int k = 0; k < lit.Length; k++) dst[k] = p[k];
        }
        return lit.Length;
    }

    // Creates or truncates a file under A:\OSB\CONF (mode 0600: root-only).
    private static void WriteConfFile(string pathLit, byte* data, int len)
    {
        char* path = stackalloc char[32];
        int pathLen = ConfPath(path, pathLit);

        // the file must exist and be a regular file; create it if missing
        uint ino;
        if (!Ext4.Resolve(path, pathLen, out ino))
        {
            char* dirPath = stackalloc char[16];
            int dlen = 0;
            SetLit(dirPath, ref dlen, "A:\\OSB\\CONF");
            uint parent;
            if (!Ext4.Resolve(dirPath, dlen, out parent)) return;

            // the base name is the tail after the last '\'
            int b = pathLen;
            while (b > 0 && path[b - 1] != '\\') b--;
            char* baseName = path + b;
            int baseLen = pathLen - b;
            if (baseLen <= 0) return;

            uint existing; byte etype;
            if (!Ext4.DirEntryExists(parent, baseName, baseLen, out existing, out etype))
            {
                if (!Ext4.AllocInode(out ino, 0x8180)) return;   // -rw-------
                if (!Ext4.DirAddEntry(parent, baseName, baseLen, ino, 1)) { Ext4.FreeInode(ino); return; }
            }
            else
            {
                ino = existing;
            }
        }
        Ext4.WriteFile(ino, data, len);
    }

    // ---- helpers ----

    private static void AddEntry(string name, string pass, int uid, string home)
    {
        char* n = stackalloc char[NameCap];
        char* p = stackalloc char[PassCap];
        char* h = stackalloc char[HomeCap];
        int nl = 0, pl = 0, hl = 0;
        SetLit(n, ref nl, name);
        SetLit(p, ref pl, pass);
        SetLit(h, ref hl, home);
        AddEntry(n, nl, p, pl, uid, h, hl);
    }

    private static void AddEntry(char* name, int nlen, char* pass, int plen, int uid, char* home, int hlen)
    {
        int idx = _users.count;
        for (int k = 0; k < nlen; k++) _users.names[idx * NameCap + k] = name[k];
        _users.nameLens[idx] = nlen;
        for (int k = 0; k < plen; k++) _users.passes[idx * PassCap + k] = pass[k];
        _users.passLens[idx] = plen;
        _users.uids[idx] = uid;
        for (int k = 0; k < hlen; k++) _users.homes[idx * HomeCap + k] = home[k];
        _users.homeLens[idx] = hlen;
        _users.sudoers[idx] = 0;
        _users.count++;
    }

    private static void CopyRow(int dst, int src)
    {
        for (int k = 0; k < NameCap; k++) _users.names[dst * NameCap + k] = _users.names[src * NameCap + k];
        for (int k = 0; k < PassCap; k++) _users.passes[dst * PassCap + k] = _users.passes[src * PassCap + k];
        for (int k = 0; k < HomeCap; k++) _users.homes[dst * HomeCap + k] = _users.homes[src * HomeCap + k];
        _users.uids[dst] = _users.uids[src];
        _users.nameLens[dst] = _users.nameLens[src];
        _users.passLens[dst] = _users.passLens[src];
        _users.homeLens[dst] = _users.homeLens[src];
        _users.sudoers[dst] = _users.sudoers[src];
    }

    private static int DecToBuf(byte* buf, uint v)
    {
        if (v == 0) { buf[0] = (byte)'0'; return 1; }
        byte* tmp = stackalloc byte[10];
        int n = 0;
        while (v != 0) { tmp[n++] = (byte)('0' + (int)(v % 10)); v /= 10; }
        for (int k = 0; k < n; k++) buf[k] = tmp[n - 1 - k];
        return n;
    }

    private static void SetLit(char* dst, ref int len, string s)
    {
        fixed (char* p = s)
        {
            for (int k = 0; k < s.Length; k++) dst[len++] = p[k];
        }
    }
}
