// OSL.CONSOLE extensions required by terminal applications such as KISS.
//
// Split out of OslangKernel.cs to keep that file navigable and within the
// project's file-size limit.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

internal static unsafe partial class Kernel
{
    // Console helpers invoked from OsStdlib. Returns true when `name` is an
    // OSL.CONSOLE function handled here.
    private static bool OsConsoleTryStdlib(char* name, int len, Expr** args, int argc, out OslVal result)
    {
        result = default;
        if (OsNameEq(name, len, "OSL.CONSOLE.WIDTH")) { result = OsNum(Vga.Width); return true; }
        if (OsNameEq(name, len, "OSL.CONSOLE.HEIGHT")) { result = OsNum(Vga.Height); return true; }
        if (OsNameEq(name, len, "OSL.CONSOLE.SIZE"))
        {
            result = OsConsoleSizeObject();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.ENTER") || OsNameEq(name, len, "OSL.CONSOLE.EXIT"))
        {
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.ALTERNATE"))
        {
            // No alternate screen buffer on VGA text mode; accept the argument
            // and keep drawing to the primary screen.
            OslVal v = (argc > 0) ? OsArg(args, argc, 0) : OsNull();
            if (_osErr) return true;
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.HIDECURSOR") || OsNameEq(name, len, "OSL.CONSOLE.SHOWCURSOR"))
        {
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.SETCURSOR"))
        {
            if (argc >= 2)
            {
                OslVal r = OsArg(args, argc, 0);
                OslVal c = OsArg(args, argc, 1);
                if (_osErr) { result = OsNull(); return true; }
                Vga.SetCursor((int)OsToNum(r, "SETCURSOR"), (int)OsToNum(c, "SETCURSOR"));
            }
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.CLEAR"))
        {
            Vga.Clear();
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.BEGINFRAME") || OsNameEq(name, len, "OSL.CONSOLE.ENDFRAME") ||
            OsNameEq(name, len, "OSL.CONSOLE.FLUSH"))
        {
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.WRITE"))
        {
            if (argc >= 3)
            {
                OslVal rr = OsArg(args, argc, 0);
                OslVal cc = OsArg(args, argc, 1);
                OslVal vv = OsArg(args, argc, 2);
                if (_osErr) { result = OsNull(); return true; }
                char* buf = stackalloc char[4096];
                int n = OsValToBuf(vv, buf, 4096);
                Vga.SetCursor((int)OsToNum(rr, "WRITE"), (int)OsToNum(cc, "WRITE"));
                Vga.WriteStr(buf, n);
            }
            else if (argc == 1)
            {
                OslVal vv = OsArg(args, argc, 0);
                if (_osErr) { result = OsNull(); return true; }
                OsPrintVal(vv);
            }
            result = OsNull();
            return true;
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.BEEP"))
        {
            result = OsNull();
            return true;
        }
        return false;
    }

    private static OslVal OsConsoleSizeObject()
    {
        int objSize = 8 + 2 * sizeof(OslVal);
        byte* mem = (byte*)OsAlloc(objSize);
        *(int*)mem = -2;          // special console-size object
        *(int*)(mem + 4) = 2;
        OslVal* fields = (OslVal*)(mem + 8);
        fields[0] = OsNum(Vga.Width);
        fields[1] = OsNum(Vga.Height);
        OslVal v = default;
        v.K = VKObj;
        v.P = (ulong)mem;
        v.L = 2;
        return v;
    }
}