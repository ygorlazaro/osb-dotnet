// OSL.APP extensions required by terminal applications such as KISS.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

internal static unsafe partial class Kernel
{
    private static bool OsAppTryStdlib(char* name, int len, Expr** args, int argc, out OslVal result)
    {
        result = default;
        if (OsNameEq(name, len, "OSL.APP.HIGHLIGHT") || OsNameEq(name, len, "OSL.APP.HIGHLIGHT.OSLANG") ||
            OsNameEq(name, len, "OSL.APP.HIGHLIGHTOSLANG"))
        {
            if (argc > 0)
            {
                OslVal v = OsArg(args, argc, 0);
                if (_osErr) { result = OsNull(); return true; }
                result = v; // passthrough highlighting
            }
            else
            {
                result = OsStr(null, 0);
            }
            return true;
        }
        if (OsNameEq(name, len, "OSL.APP.ARGS")) { result = OsStr(_osArgs, _osArgsLen); return true; }
        if (OsNameEq(name, len, "OSL.APP.NAME")) { fixed (char* p = "OSLANG") { result = OsStr(p, 6); return true; } }
        if (OsNameEq(name, len, "OSL.APP.EXIT")) { _osExit = true; result = OsNull(); return true; }
        return false;
    }
}