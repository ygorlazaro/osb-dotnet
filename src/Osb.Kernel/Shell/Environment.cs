// Session environment: the $VAR store behind SET / SET NAME=value.
//
// Fixed buffers (no managed arrays: the freestanding AOT runtime has no
// bounds-check helper). Names are stored uppercase so lookups are
// case-insensitive (DOS/OSB convention); values keep their characters.
// Redefining a variable appends its new value to the value buffer (the old
// bytes are left behind); fine for the shell-size 4 KiB buffer.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Shell;

internal static unsafe class Environment
{
    private unsafe struct EnvTable
    {
        public fixed int nameOff[Layout.MaxEnvVars];
        public fixed int nameLen[Layout.MaxEnvVars];
        public fixed int valueOff[Layout.MaxEnvVars];
        public fixed int valueLen[Layout.MaxEnvVars];
        public fixed byte names[Layout.EnvNameBufSize];
        public fixed byte values[Layout.EnvValueBufSize];
        public fixed byte locked[Layout.MaxEnvVars];  // 1 = kernel-managed, SET refuses
    }

    private static EnvTable _env;
    private static int _count;
    private static int _namePos;
    private static int _valuePos;

    // Boot defaults for the OSB session environment.
    public static void Init()
    {
        Set("PATH", "A:\\OSB\\BIN");
        Set("PROMPT", "$USER@OSB $P$G");
        Set("USER", "guest");
        Set("HOME", "A:\\OSB\\ROOT");   // root's home; login rewrites it
        Set("TEMP", "A:\\OSB\\TEMP");
        Set("COMSPEC", "A:\\OSB\\BIN\\COMMAND.COM");
        Set("OS", "OSB");
        Set("VER", "0.1.0");
        Set("LASTDRIVE", "Z");
        Lock("HOME");                     // $HOME is read-only for SET
    }

    // Sets/changes a variable (string overload for the boot defaults).
    public static bool Set(string name, string value)
    {
        fixed (char* n = name)
        fixed (char* v = value)
        {
            return SetChars(n, name.Length, v, value.Length);
        }
    }

    public static bool SetChars(char* name, int nlen, char* value, int vlen)
    {
        return SetCore(name, nlen, value, vlen, force: false);
    }

    // Kernel-internal setter (login): bypasses the read-only flag so
    // $HOME can follow the session user.
    public static bool ForceSetChars(char* name, int nlen, char* value, int vlen)
    {
        return SetCore(name, nlen, value, vlen, force: true);
    }

    // Marks an existing variable read-only (SET refuses to change it).
    public static void Lock(string name)
    {
        fixed (char* n = name)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_env.nameLen[i] != name.Length) continue;
                bool same = true;
                for (int k = 0; k < name.Length; k++)
                    if ((char)_env.names[_env.nameOff[i] + k] != n[k]) { same = false; break; }
                if (same) { _env.locked[i] = 1; return; }
            }
        }
    }

    private static bool SetCore(char* name, int nlen, char* value, int vlen, bool force)
    {
        if (nlen <= 0) return false;
        for (int i = 0; i < _count; i++)
        {
            if (_env.nameLen[i] != nlen) continue;
            bool same = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { same = false; break; }
            }
            if (!same) continue;
            if (!force && _env.locked[i] != 0) return false;
            if (_valuePos + vlen > Layout.EnvValueBufSize) return false;
            _env.valueOff[i] = _valuePos;
            _env.valueLen[i] = vlen;
            for (int k = 0; k < vlen; k++) _env.values[_valuePos + k] = (byte)value[k];
            _valuePos += vlen;
            return true;
        }
        if (_count >= Layout.MaxEnvVars) return false;
        if (_namePos + nlen > Layout.EnvNameBufSize || _valuePos + vlen > Layout.EnvValueBufSize) return false;
        int idx = _count;
        _count++;
        _env.nameOff[idx] = _namePos;
        _env.nameLen[idx] = nlen;
        for (int k = 0; k < nlen; k++)
        {
            char c = name[k];
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            _env.names[_namePos + k] = (byte)c;
        }
        _namePos += nlen;
        _env.valueOff[idx] = _valuePos;
        _env.valueLen[idx] = vlen;
        for (int k = 0; k < vlen; k++) _env.values[_valuePos + k] = (byte)value[k];
        _valuePos += vlen;
        return true;
    }

    // Case-insensitive lookup; returns the value's range in the value buffer.
    public static bool TryGet(char* name, int nlen, out int valueOff, out int valueLen)
    {
        valueOff = 0;
        valueLen = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_env.nameLen[i] != nlen) continue;
            bool same = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { same = false; break; }
            }
            if (same)
            {
                valueOff = _env.valueOff[i];
                valueLen = _env.valueLen[i];
                return true;
            }
        }
        return false;
    }

    // One character of a value, addressed by the offsets TryGet returns.
    public static char ValueAt(int off)
    {
        return (char)_env.values[off];
    }

    // Expands $VAR (names are case-insensitive) into dst. Undefined variables
    // round-trip literally, so SET PROMPT=$P$G keeps its special characters.
    public static int Expand(char* src, int slen, char* dst, int dcap)
    {
        int o = 0;
        int i = 0;
        while (i < slen && o < dcap - 1)
        {
            if (src[i] != '$') { dst[o++] = src[i++]; continue; }
            if (i + 1 >= slen) { dst[o++] = '$'; i++; continue; }
            char n0 = src[i + 1];
            bool ident = (n0 >= 'A' && n0 <= 'Z') || (n0 >= 'a' && n0 <= 'z') || n0 == '_';
            if (!ident)
            {
                dst[o++] = '$';
                i++;
                continue;
            }
            int st = i + 1;
            int j = st;
            while (j < slen && (((src[j] >= 'A' && src[j] <= 'Z') || (src[j] >= 'a' && src[j] <= 'z') ||
                                 (src[j] >= '0' && src[j] <= '9') || src[j] == '_'))) j++;
            int nameLen = j - st;
            char* nm = stackalloc char[64];
            for (int k = 0; k < nameLen && k < 63; k++) nm[k] = src[st + k];
            int vo;
            int vl;
            if (TryGet(nm, nameLen, out vo, out vl))
            {
                for (int k = 0; k < vl && o < dcap - 1; k++) dst[o++] = ValueAt(vo + k);
                i = j;
                continue;
            }
            dst[o++] = '$';
            i++;
        }
        dst[o] = '\0';
        return o;
    }

    public static void ListAll()
    {
        for (int i = 0; i < _count; i++)
        {
            for (int k = 0; k < _env.nameLen[i]; k++)
                Vga.PutChar((char)_env.names[_env.nameOff[i] + k]);
            Vga.PutChar('=');
            for (int k = 0; k < _env.valueLen[i]; k++)
                Vga.PutChar((char)_env.values[_env.valueOff[i] + k]);
            Vga.PutChar('\n');
        }
    }

    public static void ListPrefix(char* name, int nlen)
    {
        bool any = false;
        for (int i = 0; i < _count; i++)
        {
            if (_env.nameLen[i] < nlen) continue;
            bool pref = true;
            for (int k = 0; k < nlen; k++)
            {
                char c = name[k];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                if (c != (char)_env.names[_env.nameOff[i] + k]) { pref = false; break; }
            }
            if (!pref) continue;
            any = true;
            for (int k = 0; k < _env.nameLen[i]; k++)
                Vga.PutChar((char)_env.names[_env.nameOff[i] + k]);
            Vga.PutChar('=');
            for (int k = 0; k < _env.valueLen[i]; k++)
                Vga.PutChar((char)_env.values[_env.valueOff[i] + k]);
            Vga.PutChar('\n');
        }
        if (!any) Vga.WriteLine("Environment variable not defined.");
    }
}