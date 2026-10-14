// OSL.FILE extensions and helpers required by terminal applications such as KISS.
//
// Split out of OslangKernel.cs to keep that file navigable and within the
// project's file-size limit.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;
using Osb.Kernel.Shell;
using Osb.Kernel.Shell.Commands;

internal static unsafe partial class Kernel
{
    // File helpers invoked from OsStdlib. Returns true when `name` is an
    // OSL.FILE function handled here.
    private static bool OsFileTryStdlib(char* name, int len, Expr** args, int argc, out OslVal result)
    {
        result = default;
        if (OsNameEq(name, len, "OSL.FILE.READLINES"))
        {
            OslVal pathArg = OsArg(args, argc, 0);
            if (_osErr) { result = OsNull(); return true; }
            char* path = (char*)pathArg.P;
            int plen = pathArg.L;
            if (pathArg.K != VKStr) { OsFail("READLINES requires a string path."); result = OsNull(); return true; }

            uint ino;
            if (!Ext4.Resolve(path, plen, out ino)) { OsFail("File not found."); result = OsNull(); return true; }
            byte* inBuf = stackalloc byte[256];
            if (!Ext4.ReadInode(ino, inBuf)) { OsFail("Cannot read file."); result = OsNull(); return true; }
            uint sz = Ext4.InodeGetSize(inBuf);
            byte* data = stackalloc byte[4096];
            int got = 0;
            if (sz > 0) Ext4.ReadFileAll(ino, data, (sz > 4096 ? 4096 : (int)sz), out got);

            OslVal arr = OsArrMake(got / 2 + 4);
            OsArrSetCount(arr, 0);
            int start = 0;
            for (int i = 0; i <= got; i++)
            {
                bool end = (i == got) || (data[i] == (byte)'\n');
                if (!end) continue;
                int lineLen = i - start;
                if (lineLen > 0 && data[start + lineLen - 1] == (byte)'\r') lineLen--;
                char* buf = (char*)OsAlloc((lineLen + 1) * 2);
                if (buf == null) { result = OsNull(); return true; }
                for (int k = 0; k < lineLen; k++) buf[k] = (char)data[start + k];
                buf[lineLen] = '\0';
                OslVal line = OsMakeStr(buf, lineLen);
                OsArrSetCount(arr, OsArrCount(arr) + 1);
                OsArrElem(arr)[OsArrCount(arr) - 1] = line;
                start = i + 1;
            }
            if (OsArrCount(arr) == 0)
            {
                // preserve an empty single-line file
            }
            result = arr;
            return true;
        }

        if (OsNameEq(name, len, "OSL.FILE.WRITELINES"))
        {
            if (argc < 2) { OsFail("WRITELINES requires a path and an array."); result = OsNull(); return true; }
            OslVal pathArg = OsArg(args, argc, 0);
            OslVal arrArg = OsArg(args, argc, 1);
            if (_osErr || pathArg.K != VKStr || arrArg.K != VKArr) { result = OsNull(); return true; }

            char* path = (char*)pathArg.P;
            int plen = pathArg.L;
            int count = OsArrCount(arrArg);
            OslVal* elems = OsArrElem(arrArg);

            // Build joined text.
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (elems[i].K == VKStr) total += elems[i].L;
                if (i + 1 < count) total += 1;
            }
            byte* text = (byte*)OsAlloc(total + 1);
            int pos = 0;
            for (int i = 0; i < count; i++)
            {
                if (elems[i].K == VKStr)
                {
                    char* s = (char*)elems[i].P;
                    for (int k = 0; k < elems[i].L; k++) text[pos++] = (byte)s[k];
                }
                if (i + 1 < count) text[pos++] = (byte)'\n';
            }
            text[pos] = 0;

            // Resolve or create the file.
            uint ino;
            if (!Ext4.Resolve(path, plen, out ino))
            {
                char* baseName = stackalloc char[256];
                int baseLen = 0;
                uint parentIno;
                if (!FileCommands.ResolveParent(path, plen, out parentIno, baseName, ref baseLen))
                {
                    OsFail("Cannot resolve path.");
                    result = OsNull();
                    return true;
                }
                if (!Ext4.AllocInode(out ino, 0x81A4))
                {
                    OsFail("No free inode.");
                    result = OsNull();
                    return true;
                }
                Ext4.DirAddEntry(parentIno, baseName, baseLen, ino, 1);
            }

            Ext4.WriteFile(ino, text, pos);
            result = OsNull();
            return true;
        }

        // PWD is a small utility used by KISS; expose it as a global-friendly
        // name through the file helper's resolver by handling direct name match.
        if (OsNameEq(name, len, "PWD"))
        {
            byte* cwd = stackalloc byte[160];
            int cl = Cwd.CopyTo(cwd, 160);
            char* buf = (char*)OsAlloc((cl + 1) * 2);
            for (int k = 0; k < cl; k++) buf[k] = (char)cwd[k];
            buf[cl] = '\0';
            result = OsStr(buf, cl);
            return true;
        }

        return false;
    }

    // KEYCODE enum definition (0.73 name) so KISS's KEYCODE.* references work.
    public static void OsDefineKeyCodeEnum()
    {
        if (_osEnumCount >= OS_MAX_ENUMS) return;
        EnumDef* e = &_osEnums[_osEnumCount++];
        char* nameBuf = (char*)OsAlloc(8 * 2);
        fixed (char* s = "KEYCODE")
        {
            int n = 7;
            for (int i = 0; i < n; i++) nameBuf[i] = s[i];
            nameBuf[n] = '\0';
            e->N = nameBuf;
            e->NL = 7;
        }
        // Reuse the same table shape as KEY by calling the same member routine if
        // it were public; here we just copy the KEY enum if present.
        // Find the KEY enum already defined.
        int keyId = -1;
        for (int i = 0; i < _osEnumCount - 1; i++)
        {
            if (_osEnums[i].NL == 3)
            {
                fixed (char* keyName = "KEY")
                {
                    if (OsNameCI(_osEnums[i].N, _osEnums[i].NL, keyName, 3)) keyId = i;
                }
            }
        }
        if (keyId >= 0)
        {
            EnumDef* src = &_osEnums[keyId];
            e->MemberCount = src->MemberCount;
            e->Members = (char**)OsAlloc(src->MemberCount * sizeof(char*));
            e->MemberLens = (int*)OsAlloc(src->MemberCount * sizeof(int));
            e->Values = (ulong*)OsAlloc(src->MemberCount * sizeof(ulong));
            e->IsFlags = src->IsFlags;
            for (int i = 0; i < src->MemberCount; i++)
            {
                int n = src->MemberLens[i];
                char* buf = (char*)OsAlloc((n + 1) * 2);
                for (int k = 0; k < n; k++) buf[k] = src->Members[i][k];
                buf[n] = '\0';
                e->Members[i] = buf;
                e->MemberLens[i] = n;
                e->Values[i] = src->Values[i];
            }
        }
    }
}