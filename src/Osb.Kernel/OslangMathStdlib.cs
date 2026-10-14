// MATH namespace stdlib for OSLANG, used by KISS and samples.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

internal static unsafe partial class Kernel
{
    private static bool OsMathTryStdlib(char* name, int len, Expr** args, int argc, out OslVal result)
    {
        result = default;
        if (OsNameEq(name, len, "MATH.MIN") || OsNameEq(name, len, "MATH.MAX"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (_osErr || a.K != VKNum || b.K != VKNum) { result = OsNull(); return true; }
            bool max = OsNameEq(name, len, "MATH.MAX");
            result = OsNum(max ? (a.D > b.D ? a.D : b.D) : (a.D < b.D ? a.D : b.D));
            return true;
        }
        if (OsNameEq(name, len, "MATH.SQRT") || OsNameEq(name, len, "MATH.SQR"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(OsSqrt(a.D));
            return true;
        }
        if (OsNameEq(name, len, "MATH.POW"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (_osErr || a.K != VKNum || b.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(OsPow(a.D, b.D));
            return true;
        }
        if (OsNameEq(name, len, "MATH.ABS"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(a.D < 0 ? -a.D : a.D);
            return true;
        }
        if (OsNameEq(name, len, "MATH.FLOOR"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(OsFloor(a.D));
            return true;
        }
        if (OsNameEq(name, len, "MATH.CEIL"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(-OsFloor(-a.D));
            return true;
        }
        if (OsNameEq(name, len, "MATH.SIGN"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(a.D > 0 ? 1 : (a.D < 0 ? -1 : 0));
            return true;
        }
        if (OsNameEq(name, len, "MATH.ROUND"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr || a.K != VKNum) { result = OsNull(); return true; }
            result = OsNum(OsFloor(a.D + 0.5));
            return true;
        }
        if (OsNameEq(name, len, "MATH.CLAMP"))
        {
            OslVal v = OsArg(args, argc, 0);
            OslVal lo = OsArg(args, argc, 1);
            OslVal hi = OsArg(args, argc, 2);
            if (_osErr || v.K != VKNum || lo.K != VKNum || hi.K != VKNum) { result = OsNull(); return true; }
            double r = v.D;
            if (r < lo.D) r = lo.D;
            if (r > hi.D) r = hi.D;
            result = OsNum(r);
            return true;
        }
        if (OsNameEq(name, len, "MATH.PI"))
        {
            result = OsNum(3.141592653589793);
            return true;
        }
        if (OsNameEq(name, len, "MATH.PI")) { result = OsNum(OsPi()); return true; }
        if (OsNameEq(name, len, "MATH.E")) { result = OsNum(2.718281828459045); return true; }
        if (OsNameEq(name, len, "MATH.SIN")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsSin(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.COS")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsCos(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.TAN")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} double c=OsCos(a.D); result=OsNum(c==0?0:OsSin(a.D)/c); return true; }
        if (OsNameEq(name, len, "MATH.ASIN")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsAsin(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.ACOS")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsAcos(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.ATAN")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsAtan(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.ATAN2")) {
            OslVal a=OsArg(args,argc,0); OslVal b=OsArg(args,argc,1);
            if(_osErr||a.K!=VKNum||b.K!=VKNum){result=OsNull();return true;}
            result=OsNum(OsAtan2(a.D,b.D)); return true;
        }
        if (OsNameEq(name, len, "MATH.LOG")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum||a.D<=0){result=OsNull();return true;} result=OsNum(OsLn(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.LOG10")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum||a.D<=0){result=OsNull();return true;} result=OsNum(OsLn(a.D)/OsLn(10.0)); return true; }
        if (OsNameEq(name, len, "MATH.EXP")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum(OsExp(a.D)); return true; }
        if (OsNameEq(name, len, "MATH.TRUNC")) { OslVal a=OsArg(args,argc,0); if(_osErr||a.K!=VKNum){result=OsNull();return true;} result=OsNum((double)(long)a.D); return true; }
        if (OsNameEq(name, len, "MATH.MOD")) {
            OslVal a=OsArg(args,argc,0); OslVal b=OsArg(args,argc,1);
            if(_osErr||a.K!=VKNum||b.K!=VKNum||b.D==0){result=OsNull();return true;}
            result=OsNum(a.D - OsFloor(a.D / b.D) * b.D);
            return true;
        }
        if (OsNameEq(name, len, "MATH.ROUND")) {
            OslVal a=OsArg(args,argc,0);
            if(_osErr||a.K!=VKNum){result=OsNull();return true;}
            if (argc > 1)
            {
                OslVal d=OsArg(args,argc,1);
                if (d.K!=VKNum) { result=OsNull(); return true; }
                double scale=OsPow(10.0,d.D);
                result=OsNum(OsFloor(a.D*scale+0.5)/scale);
                return true;
            }
            result=OsNum(OsFloor(a.D+0.5));
            return true;
        }
        if (OsNameEq(name, len, "MATH.RANDOM")) {
            uint seed = 12345;
            seed = (uint)(seed * 1664525u + 1013904223u);
            if (argc >= 2) {
                OslVal lo=OsArg(args,argc,0); OslVal hi=OsArg(args,argc,1);
                if(_osErr||lo.K!=VKNum||hi.K!=VKNum){result=OsNull();return true;}
                int left=(int)lo.D, right=(int)hi.D, span = right-left+1;
                if (span <= 0) { result=OsNull(); return true; }
                result=OsNum(left + (int)((seed >> 8) % (uint)span));
                return true;
            }
            result=OsNum((seed >> 8) / 16777216.0);
            return true;
        }
        return false;
    }
}