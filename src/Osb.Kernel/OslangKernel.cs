// OSLANG 0.72 interpreter for the OSB freestanding kernel.
//
// The language (lexer, parser, semantics) follows src/Osb.Lang (the
// reference implementation). This is a from-scratch reimplementation
// for the bare-metal kernel: no BCL, no GC, no exceptions. Every
// dynamic structure (tokens, AST, strings, arrays, scopes, function
// table) is carved from a single bump allocator over a frame-allocated
// arena, so a script run is one arena slice that is reset when the
// script ends.
//
// Supported subset (the OSB kernel target):
//   PRINT / SHOW / INPUT / CLEAR, IF..THEN..ELIF..ELSE..END,
//   WHILE..END, FOR..TO..STEP..END, FUNCTION..END FUNCTION, RETURN,
//   BREAK, CONTINUE, VAR/GLOBAL, arrays (literals, index get/set),
//   strings (literals, escapes, ${} interpolation, + concat),
//   numbers, booleans, NULL, and the OSL.CONSOLE / OSL.FILE / OSL.APP
//   APIs plus the standard math/conversion functions.

using System;
using System.Runtime.CompilerServices;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Storage;

internal static unsafe partial class Kernel
{
    // ==================================================================
    //  Value model
    // ==================================================================
    private const byte VKNull = 0, VKBool = 1, VKNum = 2, VKStr = 3, VKArr = 4, VKObj = 5, VKEnum = 6, VKFunc = 7;
    private const byte VIS_PUBLIC = 0, VIS_PRIVATE = 1, VIS_PROTECTED = 2;

    // A string is (char* P, int L) into the arena. An array is a header
    // [int cap][int count][OslVal elements...] also in the arena; P
    // points at the header.
    // An object is a header [int classId][int fieldCount][OslVal fields...]
    // An enum value is [ulong rawValue][int enumTypeId]
    private struct OslVal
    {
        public byte K;
        public bool B;
        public double D;
        public ulong P;
        public int L;
    }

    private struct VarEntry
    {
        public char* N;
        public int NL;
        public OslVal V;
    }

    // Scope chain: entries live in the arena; Parent is a Scope* (0 = none).
    private struct Scope
    {
        public VarEntry* E;
        public int C;
        public int Cap;
        public ulong Parent;
    }

    private struct FuncDef
    {
        public char* N;
        public int NL;
        public char** P;      // parameter name pointers (into source)
        public int* PL;       // parameter name lengths
        public int PC;
        public Stmt* Body;
        public int BL;
    }

private struct ClassDef
    {
        public char* N;
        public int NL;
        public char** Fields;
        public int* FieldLens;
        public int FieldCount;
        public char** Methods;
        public Stmt** MethodBodies;
        public int* MethodBodyLens;
        public char*** MethodParamNames;  // array of param name arrays per method
        public int** MethodParamLens;     // array of param len arrays per method
        public int* MethodParamCounts;    // param count per method
        public int MethodCount;
        public int ParentClassId;
    }

    private struct EnumDef
    {
        public char* N;
        public int NL;
        public char** Members;
        public int* MemberLens;
        public ulong* Values;     // raw values
        public int MemberCount;
        public bool IsFlags;
    }

    private struct InterfaceDef
    {
        public char* N;
        public int NL;
        public char** Members;
        public int* MemberLens;
        public int MemberCount;
    }

    private const int OS_MAX_INTERFACES = 32;
    private static int _osInterfaceCount = 0;
    private static InterfaceDef* _osInterfaces;

    // ---- runtime budgets ----
    // How much of the 4 MiB arena the script source itself may occupy. The rest
    // has to hold the token array, the AST, scopes and every string the run
    // allocates, so the source gets a conservative share (B6).
    private const ulong MaxScriptBytes = 512UL * 1024UL;

    // Deepest call chain allowed (functions and methods share the counter).
    //
    // The interpreter recurses natively: one OSLANG call level is a chain of
    // C# frames (OsEvalCall -> OsCallFunction -> OsExecBlock -> OsExec ->
    // OsEval -> OsEvalBin -> ...), a few hundred bytes of CPU stack per level.
    // The shell runs on the 16 KiB boot stack (boot.S) and there is no guard page
    // below it, so the limit is set from that budget rather than from the arena.
    //
    // Measured on this build: a plain recursive call survived 28 levels and hung at 32,
    // but the same depth inside a TRY crashed the machine, because unwinding to the
    // CATCH handler needs stack on top of the deepest frame. 12 leaves a wide margin
    // under both cases - deep enough for any real script, shallow enough that a
    // runaway recursion always reports "Recursion too deep" as a catchable error
    // instead of smashing the stack (B7). Raising it means giving the interpreter its
    // own kernel stack (a scheduler slot), not just editing this number.
    private const int MaxCallDepth = 12;
    private static int _osCallDepth;

    // ==================================================================
    //  Tokens
    // ==================================================================
    private enum Tk : byte
    {
        Eof = 0, Newline,
        Id, Num, Str,
        Print, Show, Input, Clear,
        If, Then, Elif, Else, End,
        For, To, Step, While, Do,
        Function, Return, Var, Global,
        True, False, Null,
        And, Or, Not, Mod,
        Plus, Minus, Star, Slash, Percent, StarStar,
        PlusEqual, PlusPlus, MinusMinus,
        Equal, NotEqual, Less, Greater, LessEqual, GreaterEqual,
        LParen, RParen, LBracket, RBracket, LBrace, RBrace,
        Comma, Colon, Dot, DoubleArrow, Pipe,
        Using, Me, Base, New, Class, Interface, Enum, Event,
        Break, Continue,
        Public, Private, Protected,
        Try, Catch,
        Constructor,
        None = 250
    }

    private struct Tok
    {
        public Tk K;
        public char* T;
        public int L;
        public int Line; // 1-based source line
        public double N;
    }

    // ==================================================================
    //  AST
    // ==================================================================
    private enum Ek : byte
    {
        Num, Str, Bool, Null, Id,
        Bin, Un, Call, Member, Index, Arr, Interp,
        NewObj, This, EnumVal, ClassRef, FuncRef
    }

    private const byte OP_ADD = 1, OP_SUB = 2, OP_MUL = 3, OP_DIV = 4,
        OP_MOD = 5, OP_POW = 6, OP_EQ = 7, OP_NE = 8, OP_LT = 9,
        OP_GT = 10, OP_LE = 11, OP_GE = 12, OP_AND = 13, OP_OR = 14,
        OP_NEG = 15, OP_NOT = 16, OP_BOR = 17;

    private struct Expr
    {
        public Ek K;
        public OslVal V;              // literal value (Num/Str/Bool/Null)
        public char* N; public int NL; // Id name / Member name
        public byte Op;               // Bin/Un operator
        public Expr* A;               // Bin.left / Un.operand / Call.callee /
                                      // Member.object / Index.array
        public Expr* B;               // Bin.right / Index.index
        public Expr** Args; public int AC; // Call.args / Arr.elements / Interp.parts
    }

    private enum Sk : byte
    {
        Nop, Print, Show, Input, Clear,
        Assign, AddAssign, Expr,
        If, While, For, Return, Var, Global, Break, Continue,
        ClassDecl, EnumDecl, MethodDecl,
        DoWhile, TryCatch
    }

    private struct Stmt
    {
        public Sk K;
        public char* N; public int NL;   // var name / func name / input target
        public Expr* E1;                 // if/while cond, assign target, for from, return value
        public Expr* E2;                 // assign value, for to
        public Expr* E3;                 // for step
        public Expr** Args; public int AC; // print/show expression list
        public Stmt* Body; public int BL;   // block body
        public Stmt* ElseBody; public int EL; // if else branch
        public char** P; public int PC;   // function parameter names
    }

    // ==================================================================
    //  Arena + interpreter state
    // ==================================================================
    private static byte* _osHeap;
    private static ulong _osHeapUsed, _osHeapSize;

    private static Tok* _osToks;
    private static int _osTokCount, _osPos;

    private static FuncDef* _osFuncs;
    private static int _osFuncCount;
    private const int OS_MAX_FUNCS = 512;

    private static ClassDef* _osClasses;
    private static int _osClassCount;
    private const int OS_MAX_CLASSES = 64;

    private static EnumDef* _osEnums;
    private static int _osEnumCount;
    private const int OS_MAX_ENUMS = 32;

    private static ulong _osScope;       // current Scope*
    private static ulong _osGlobalScope; // global Scope*
    private static int _osCurrentClassId; // for ME resolution in methods
    private static int _osCurrentEnumId;  // for enum member access

    private const byte CTL_NONE = 0, CTL_BREAK = 1, CTL_CONTINUE = 2, CTL_RETURN = 3, CTL_EXCEPTION = 4;
    private static byte _osCtl;
    private static OslVal _osRet;
    private static bool _osExit;
    private static char* _osErrMsgPtr;     // message for CATCH (static buffer)
    private static int _osErrMsgLen;
    private static bool _osErr;
    private static char* _osArgs;
    private static char* _osCurrentMethodName;
    private static int _osCurrentMethodNameLen;
    private static char* _osScriptPath;
    private static int _osScriptPathLen;
    private static int _osArgsLen;

    // ==================================================================
    //  Arena allocator
    // ==================================================================
    private static void* OsAlloc(int bytes)
    {
        bytes = (bytes + 7) & ~7;
        if (_osHeapUsed + (ulong)bytes > _osHeapSize)
        {
            OsFail("Out of memory (script too large).");
            return null;
        }
        void* p = _osHeap + _osHeapUsed;
        _osHeapUsed += (ulong)bytes;
        return p;
    }

    private static Expr* OsAllocExpr() => (Expr*)OsAlloc(sizeof(Expr));
    private static Expr** OsAllocExprArr(int n) => (Expr**)OsAlloc(n * sizeof(Expr*));
    private static Stmt* OsAllocStmtArr(int n) => (Stmt*)OsAlloc(n * sizeof(Stmt));

    // ==================================================================
    //  Errors (no exceptions: set a flag, propagate via entry checks)
    // ==================================================================
    // Error messages live in a static buffer, not in the arena: the error path
    // has to work when the arena is exhausted (which is often why the error
    // fired in the first place), and OsFail must never allocate or recurse.
    private const int OsErrMsgCap = 192;
    private unsafe struct ErrMsg { public fixed char text[OsErrMsgCap]; }
    private static ErrMsg _osErrMsg;

    private static void OsErrAppendStr(string s)
    {
        fixed (char* dst = _osErrMsg.text)
        fixed (char* src = s)
        {
            for (int i = 0; i < s.Length && _osErrMsgLen < OsErrMsgCap - 1; i++)
                dst[_osErrMsgLen++] = src[i];
            dst[_osErrMsgLen] = '\0';
            _osErrMsgPtr = dst;   // static storage: stays valid after the block
        }
    }

    private static void OsErrAppendChars(char* s, int len)
    {
        fixed (char* dst = _osErrMsg.text)
        {
            for (int i = 0; i < len && _osErrMsgLen < OsErrMsgCap - 1; i++)
                dst[_osErrMsgLen++] = s[i];
            dst[_osErrMsgLen] = '\0';
            _osErrMsgPtr = dst;
        }
    }

    // Raises a catchable runtime error: every OsFail* variant must go through
    // here so that CATCH sees CTL_EXCEPTION and a message (B4).
    private static void OsRaise(string msg)
    {
        _osErr = true;
        _osCtl = CTL_EXCEPTION;
        _osErrMsgLen = 0;
        OsErrAppendStr(msg);
    }

    private static void OsFail(string msg)
    {
        if (_osErr) return;
        OsRaise(msg);
        Vga.Write("OSLANG error");
        if (_osScriptPath != null && _osScriptPathLen > 0)
        {
            Vga.Write(" in ");
            Vga.WriteStr(_osScriptPath, _osScriptPathLen);
            if (_osTokCount > 0 && _osPos < _osTokCount) Vga.WriteDec((ulong)_osToks[_osPos].Line);
            Vga.PutChar(':');
        }
        Vga.Write(": ");
        Vga.WriteLine(msg);
    }

    private static void OsFailName(char* name, int len, string prefix)
    {
        if (_osErr) return;
        OsRaise("");
        OsErrAppendStr(prefix);
        OsErrAppendChars(name, len);
        OsErrAppendStr("'.");
        Vga.Write("OSLANG error");
        if (_osScriptPath != null && _osScriptPathLen > 0)
        {
            Vga.Write(" in ");
            Vga.WriteStr(_osScriptPath, _osScriptPathLen);
            if (_osTokCount > 0 && _osPos < _osTokCount) Vga.WriteDec((ulong)_osToks[_osPos].Line);
            Vga.PutChar(':');
        }
        Vga.Write(": ");
        Vga.Write(prefix);
        for (int i = 0; i < len; i++) Vga.PutChar(name[i]);
        Vga.WriteLine("'.");
    }

    private static void OsFail2(string a, string b)
    {
        if (_osErr) return;
        OsRaise(a);
        OsErrAppendStr(b);
        Vga.Write("OSLANG error: ");
        Vga.Write(a);
        Vga.WriteLine(b);
    }

    // ==================================================================
    //  Value constructors / helpers
    // ==================================================================
    private static OslVal OsNull() { OslVal v = default; v.K = VKNull; return v; }
    private static OslVal OsBool(bool b) { OslVal v = default; v.K = VKBool; v.B = b; return v; }
    private static OslVal OsNum(double d) { OslVal v = default; v.K = VKNum; v.D = d; return v; }

    private static OslVal OsStr(char* src, int len)
    {
        char* buf = (char*)OsAlloc(len * 2 + 2);
        if (buf == null) return OsNull(); // arena exhausted; OsFail already raised
        for (int i = 0; i < len; i++) buf[i] = src[i];
        OslVal v = default;
        v.K = VKStr; v.P = (ulong)buf; v.L = len;
        return v;
    }

    private static bool OsTruthy(OslVal v)
    {
        switch (v.K)
        {
            case VKNull: return false;
            case VKBool: return v.B;
            case VKNum: return v.D != 0;
            case VKStr: return v.L > 0;
            default: return true; // array / object -> truthy
        }
    }

    private static bool OsValuesEqual(OslVal a, OslVal b)
    {
        if (a.K == VKNull || b.K == VKNull) return a.K == VKNull && b.K == VKNull;
        if (a.K != b.K) return false;
        switch (a.K)
        {
            case VKNum: return a.D == b.D;
            case VKStr: return OsStrEq(a, b);
            case VKBool: return a.B == b.B;
            default: return a.P == b.P; // arrays/objects: identity
        }
    }

    private static bool OsStrEq(OslVal a, OslVal b)
    {
        if (a.L != b.L) return false;
        char* x = (char*)a.P; char* y = (char*)b.P;
        for (int i = 0; i < a.L; i++) if (x[i] != y[i]) return false;
        return true;
    }

    // Case-insensitive identifier comparison against a literal.
    private static bool OsNameEq(char* s, int len, string lit)
    {
        if (len != lit.Length) return false;
        fixed (char* q = lit)
        {
            for (int i = 0; i < len; i++)
            {
                char a = s[i];
                if (a >= 'a' && a <= 'z') a -= (char)32;
                if (a != q[i]) return false;
            }
        }
        return true;
    }

    // Case-insensitive comparison of two (char*, int) names.
    private static bool OsNameCI(char* a, int al, char* b, int bl)
    {
        if (al != bl) return false;
        for (int i = 0; i < al; i++)
        {
            char x = a[i], y = b[i];
            if (x >= 'a' && x <= 'z') x -= (char)32;
            if (y >= 'a' && y <= 'z') y -= (char)32;
            if (x != y) return false;
        }
        return true;
    }

    // ==================================================================
    //  Number <-> string (manual, no BCL)
    // ==================================================================
    private static double OsFloor(double v)
    {
        long t = (long)v;
        if (v >= 0 || (double)t == v) return (double)t;
        return (double)(t - 1);
    }

    private static double OsAbs(double v) => v < 0 ? -v : v;

    private static double OsSqrt(double v)
    {
        if (v <= 0) return 0;
        double x = v;
        for (int i = 0; i < 40; i++) x = 0.5 * (x + v / x);
        return x;
    }

    private static double OsPowInt(double b, long e)
    {
        double r = 1;
        while (e > 0) { if ((e & 1) != 0) r *= b; b *= b; e >>= 1; }
        return r;
    }

    private static double OsPow(double b, double e)
    {
        if (e == (double)(long)e && OsAbs(e) < 1e9)
        {
            long n = (long)e;
            if (n >= 0) return OsPowInt(b, n);
            return 1 / OsPowInt(b, -n);
        }
        if (b <= 0) return 0;
        // exp(e*ln(b)) via Taylor series
        double x = (b - 1) / (b + 1);
        double ln = 0;
        for (long i = 1; i < 60; i += 2) ln += (1.0 / i) * OsPowInt(x, i);
        ln *= 2;
        double y = e * ln, r = 1, term = 1;
        for (int i = 1; i < 90; i++) { term *= y / i; r += term; }
        return r;
    }

    private static int OsCopyLit(char* buf, int cap, string lit)
    {
        int n = 0;
        fixed (char* q = lit)
            while (n < cap && n < lit.Length) { buf[n] = q[n]; n++; }
        return n;
    }

    // Format a double into buf (max cap), return length. Integers print
    // without a fractional part ("40", not "40.0").
    private static int OsFormatDouble(double v, char* buf, int cap)
    {
        int p = 0;
        if (v != v) return OsCopyLit(buf, cap, "NaN"); // NaN is not equal to itself
        if (v < 0) { if (p < cap) buf[p] = '-'; p++; v = -v; }
        bool integer = (v == OsFloor(v)) && OsAbs(v) < 1e15;
        long ip = (long)v;
        char* tmp = stackalloc char[24];
        int tn = 0;
        if (ip == 0) tmp[tn++] = '0';
        while (ip > 0) { tmp[tn++] = (char)('0' + (int)(ip % 10)); ip /= 10; }
        while (tn > 0 && p < cap) buf[p++] = tmp[--tn];
        if (!integer)
        {
            if (p < cap) buf[p] = '.'; p++;
            double frac = v - OsFloor(v);
            for (int i = 0; i < 12 && p < cap; i++)
            {
                frac *= 10;
                int d = (int)OsFloor(frac);
                frac -= d;
                buf[p++] = (char)('0' + d);
            }
            while (p > 0 && buf[p - 1] == '0') p--;
            if (p > 0 && buf[p - 1] == '.') p--;
        }
        return p;
    }

    private static double OsParseNumber(char* s, int len)
    {
        double result = 0;
        int i = 0;
        bool neg = false;
        if (i < len && s[i] == '-') { neg = true; i++; }
        else if (i < len && s[i] == '+') { i++; }
        while (i < len && s[i] >= '0' && s[i] <= '9')
        {
            result = result * 10 + (s[i] - '0');
            i++;
        }
        if (i < len && s[i] == '.')
        {
            i++;
            double scale = 0.1;
            while (i < len && s[i] >= '0' && s[i] <= '9')
            {
                result += (s[i] - '0') * scale;
                scale *= 0.1;
                i++;
            }
        }
        return neg ? -result : result;
    }

    // ==================================================================
    //  Display / concatenation
    // ==================================================================
    private static int OsDisplayLen(OslVal v)
    {
        switch (v.K)
        {
            case VKNull: return 4;              // "NULL"
            case VKStr: return v.L;
            case VKBool: return v.B ? 4 : 5;    // TRUE/FALSE
            case VKNum: { char* t = stackalloc char[40]; return OsFormatDouble(v.D, t, 40); }
            case VKEnum:
            {
                // Estimate display length - use raw value if no member matches
                int enumId = v.L;
                if (enumId >= 0 && enumId < _osEnumCount)
                {
                    EnumDef* en = &_osEnums[enumId];
                    for (int i = 0; i < en->MemberCount; i++)
                    {
                        if (en->Values[i] == v.P)
                        {
                            return en->MemberLens[i];
                        }
                    }
                }
                // Fallback: estimate length of raw number
                char* t = stackalloc char[40];
                return OsFormatDouble((double)v.P, t, 40);
            }
            default: return 0;
        }
    }

    private static int OsWriteDisplay(char* buf, OslVal v)
    {
        switch (v.K)
        {
            case VKNull: return OsCopyLit(buf, 16, "NULL");
            case VKStr:
            {
                char* s = (char*)v.P;
                for (int i = 0; i < v.L; i++) buf[i] = s[i];
                return v.L;
            }
            case VKBool: return OsCopyLit(buf, 16, v.B ? "TRUE" : "FALSE");
            case VKNum: return OsFormatDouble(v.D, buf, 40);
            case VKEnum:
            {
                int enumId = v.L;
                if (enumId >= 0 && enumId < _osEnumCount)
                {
                    EnumDef* en = &_osEnums[enumId];
                    for (int i = 0; i < en->MemberCount; i++)
                    {
                        if (en->Values[i] == v.P)
                        {
                            for (int j = 0; j < en->MemberLens[i]; j++) buf[j] = en->Members[i][j];
                            return en->MemberLens[i];
                        }
                    }
                }
                // Fallback: print raw value
                return OsFormatDouble((double)v.P, buf, 40);
            }
            default: return 0;
        }
    }

    private static void OsPrintVal(OslVal v)
    {
        switch (v.K)
        {
            case VKNull: Vga.Write("NULL"); break;
            case VKStr:
            {
                char* s = (char*)v.P;
                for (int i = 0; i < v.L; i++) Vga.PutChar(s[i]);
                break;
            }
            case VKBool: Vga.Write(v.B ? "TRUE" : "FALSE"); break;
            case VKNum:
            {
                char* buf = stackalloc char[40];
                int n = OsFormatDouble(v.D, buf, 40);
                for (int i = 0; i < n; i++) Vga.PutChar(buf[i]);
                break;
            }
            case VKEnum:
            {
                // Print enum member name
                int enumId = v.L;
                bool found = false;
                if (enumId >= 0 && enumId < _osEnumCount)
                {
                    EnumDef* en = &_osEnums[enumId];
                    for (int i = 0; i < en->MemberCount; i++)
                    {
                        if (en->Values[i] == v.P)
                        {
                            for (int j = 0; j < en->MemberLens[i]; j++) Vga.PutChar(en->Members[i][j]);
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    // Print raw value
                    char* buf = stackalloc char[40];
                    int n = OsFormatDouble((double)v.P, buf, 40);
                    for (int i = 0; i < n; i++) Vga.PutChar(buf[i]);
                }
                break;
            }
            default: break;
        }
    }

    private static OslVal OsConcat(OslVal a, OslVal b)
    {
        int la = OsDisplayLen(a), lb = OsDisplayLen(b);
        char* buf = (char*)OsAlloc((la + lb) * 2 + 2);
        int p = OsWriteDisplay(buf, a);
        OsWriteDisplay(buf + p, b);
        OslVal v = default;
        v.K = VKStr; v.P = (ulong)buf; v.L = la + lb;
        return v;
    }

    // Convert any value to its display string (allocated in the arena).
    private static OslVal OsToDisplayStr(OslVal v)
    {
        if (v.K == VKStr) return v;
        int len = OsDisplayLen(v);
        char* buf = (char*)OsAlloc(len * 2 + 2);
        OsWriteDisplay(buf, v);
        OslVal r = default;
        r.K = VKStr; r.P = (ulong)buf; r.L = len;
        return r;
    }

    // ==================================================================
    //  Arrays
    // ==================================================================
    private static OslVal OsArrMake(int cap)
    {
        if (cap < 0) cap = 0;
        byte* mem = (byte*)OsAlloc(8 + cap * sizeof(OslVal));
        if (mem == null) return OsNull(); // arena exhausted; OsFail already raised
        *(int*)mem = cap;                 // capacity
        *(int*)(mem + 4) = 0;             // count, published by OsArrSetCount
        OslVal v = default;
        v.K = VKArr; v.P = (ulong)mem; v.L = 0;
        return v;
    }

    private static void OsArrEnsure(OslVal arr, int extra) { /* see array growth comment; no-op in this minimal port */ }
    private static int OsArrCount(OslVal a) => a.K == VKArr ? *(int*)((byte*)a.P + 4) : 0;
    private static OslVal* OsArrElem(OslVal a) => (OslVal*)((byte*)a.P + 8);
    private static void OsArrSetCount(OslVal a, int n) { if (a.K == VKArr) *(int*)((byte*)a.P + 4) = n; }

    // ==================================================================
    //  LEXER
    // ==================================================================
    private static bool OsIsIdentStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    private static bool OsIsIdentPart(char c) => OsIsIdentStart(c) || (c >= '0' && c <= '9');

    private static int OsLex(char* src, int len, Tok* toks, int maxToks)
    {
        int n = 0;
        int i = 0;
        while (i < len)
        {
            char c = src[i];
            if (c == ' ' || c == '\t' || c == '\r') { i++; continue; }
            if (c == '\n')
            {
                if (n >= maxToks) { OsFail("Too many tokens."); return n; }
                toks[n].K = Tk.Newline; toks[n].T = src + i; toks[n].L = 1; n++;
                i++;
                continue;
            }
            if (c == '\'') // comment to end of line
            {
                while (i < len && src[i] != '\n') i++;
                continue;
            }
            if ((c == 'r' || c == 'R') && i + 3 <= len &&
                (src[i + 1] == 'e' || src[i + 1] == 'E') &&
                (src[i + 2] == 'm' || src[i + 2] == 'M') &&
                (i + 3 == len || src[i + 3] == ' ' || src[i + 3] == '\t' || src[i + 3] == '\n'))
            {
                while (i < len && src[i] != '\n') i++;
                continue;
            }
            if (c >= '0' && c <= '9')
            {
                int start = i;
                while (i < len && src[i] >= '0' && src[i] <= '9') i++;
                if (i < len && src[i] == '.' && i + 1 < len && src[i + 1] >= '0' && src[i + 1] <= '9')
                {
                    i++;
                    while (i < len && src[i] >= '0' && src[i] <= '9') i++;
                }
                if (n >= maxToks) { OsFail("Too many tokens."); return n; }
                toks[n].K = Tk.Num;
                toks[n].T = src + start;
                toks[n].L = i - start;
                toks[n].N = OsParseNumber(src + start, i - start);
                n++;
                continue;
            }
            if (c == '"')
            {
                int rstart = i + 1;
                int j = rstart;
                while (j < len && src[j] != '"' && src[j] != '\n' && src[j] != '\r')
                {
                    if (src[j] == '\\' && j + 1 < len) j++;
                    j++;
                }
                int maxLen = j - rstart;
                char* buf = (char*)OsAlloc(maxLen * 2 + 2);
                int bl = 0;
                i = rstart;
                while (i < len && src[i] != '"' && src[i] != '\n' && src[i] != '\r')
                {
                    if (src[i] == '\\' && i + 1 < len)
                    {
                        char esc = src[i + 1];
                        if (esc == 'n') { buf[bl++] = '\n'; i += 2; }
                        else if (esc == 't') { buf[bl++] = '\t'; i += 2; }
                        else if (esc == '\\') { buf[bl++] = '\\'; i += 2; }
                        else if (esc == '"') { buf[bl++] = '"'; i += 2; }
                        else if (esc == '$') { buf[bl++] = '\\'; buf[bl++] = '$'; i += 2; }
                        else { OsFail("Unknown escape sequence."); i += 2; }
                    }
                    else { buf[bl++] = src[i]; i++; }
                }
                if (i < len && src[i] == '"') i++; // closing quote
                if (n >= maxToks) { OsFail("Too many tokens."); return n; }
                toks[n].K = Tk.Str; toks[n].T = buf; toks[n].L = bl; n++;
                continue;
            }
            if (OsIsIdentStart(c))
            {
                int start = i;
                while (i < len && OsIsIdentPart(src[i])) i++;
                if (n >= maxToks) { OsFail("Too many tokens."); return n; }
                if (n > 0 && toks[n - 1].K == Tk.Dot)
                {
                    // Allow reserved words as member names after a dot (e.g.
                    // KEYCODE.END), matching the OSLANG specification.
                    toks[n].K = Tk.Id;
                }
                else
                {
                    toks[n].K = OsKeyword(src + start, i - start);
                }
                toks[n].T = src + start;
                toks[n].L = i - start;
                n++;
                continue;
            }
            if (n >= maxToks) { OsFail("Too many tokens."); return n; }
            int adv = 1;
            Tk k = Tk.Eof;
            switch (c)
            {
                case '+':
                    if (i + 1 < len && src[i + 1] == '+') { k = Tk.PlusPlus; adv = 2; }
                    else if (i + 1 < len && src[i + 1] == '=') { k = Tk.PlusEqual; adv = 2; }
                    else k = Tk.Plus;
                    break;
                case '-':
                    if (i + 1 < len && src[i + 1] == '-') { k = Tk.MinusMinus; adv = 2; }
                    else k = Tk.Minus;
                    break;
                case '*':
                    if (i + 1 < len && src[i + 1] == '*') { k = Tk.StarStar; adv = 2; }
                    else k = Tk.Star;
                    break;
                case '/': k = Tk.Slash; break;
                case '%': k = Tk.Percent; break;
                case '(': k = Tk.LParen; break;
                case ')': k = Tk.RParen; break;
                case '[': k = Tk.LBracket; break;
                case ']': k = Tk.RBracket; break;
                case '{': k = Tk.LBrace; break;
                case '}': k = Tk.RBrace; break;
                case ',': k = Tk.Comma; break;
                case ':': k = Tk.Colon; break;
                case '.': k = Tk.Dot; break;
                case ';': k = Tk.Newline; break; // semicolon as statement separator
                case '=':
                    if (i + 1 < len && src[i + 1] == '>') { k = Tk.DoubleArrow; adv = 2; }
                    else k = Tk.Equal;
                    break;
                case '<':
                    if (i + 1 < len && src[i + 1] == '>') { k = Tk.NotEqual; adv = 2; }
                    else if (i + 1 < len && src[i + 1] == '=') { k = Tk.LessEqual; adv = 2; }
                    else k = Tk.Less;
                    break;
                case '>':
                    if (i + 1 < len && src[i + 1] == '=') { k = Tk.GreaterEqual; adv = 2; }
                    else k = Tk.Greater;
                    break;
                case '|': k = Tk.Pipe; break;
                default:
                    OsFail("Unexpected character in source.");
                    return n;
            }
            toks[n].K = k; toks[n].T = src + i; toks[n].L = adv; n++;
            i += adv;
            continue;
        }
        if (n >= maxToks) { OsFail("Too many tokens."); return n; }
        toks[n].K = Tk.Eof; toks[n].T = src + len; toks[n].L = 0; n++;
        // Assign source line numbers from token start offsets.
        int _row = 1;
        char* _p = src;
        for (int _i = 0; _i < n; _i++)
        {
            while (_p < toks[_i].T && _p < src + len)
            {
                if (*_p == '\n') _row++;
                _p++;
            }
            toks[_i].Line = _row;
        }
        return n;
    }

    // Map an identifier to its keyword (case-insensitive), or Id.
    private static Tk OsKeyword(char* s, int len)
    {
        switch (len)
        {
            case 2:
                if (OsNameEq(s, len, "IF")) return Tk.If;
                if (OsNameEq(s, len, "TO")) return Tk.To;
                if (OsNameEq(s, len, "DO")) return Tk.Do;
                if (OsNameEq(s, len, "OR")) return Tk.Or;
                if (OsNameEq(s, len, "ME")) return Tk.Me;
                break;
case 3:
                if (OsNameEq(s, len, "FOR")) return Tk.For;
                if (OsNameEq(s, len, "END")) return Tk.End;
                if (OsNameEq(s, len, "VAR")) return Tk.Var;
                if (OsNameEq(s, len, "NOT")) return Tk.Not;
                if (OsNameEq(s, len, "AND")) return Tk.And;
                if (OsNameEq(s, len, "NEW")) return Tk.New;
                if (OsNameEq(s, len, "MOD")) return Tk.Mod;
                if (OsNameEq(s, len, "TRY")) return Tk.Try;
                break;
            case 4:
                if (OsNameEq(s, len, "THEN")) return Tk.Then;
                if (OsNameEq(s, len, "ELSE")) return Tk.Else;
                if (OsNameEq(s, len, "ELIF")) return Tk.Elif;
                if (OsNameEq(s, len, "TRUE")) return Tk.True;
                if (OsNameEq(s, len, "NULL")) return Tk.Null;
                if (OsNameEq(s, len, "SHOW")) return Tk.Show;
                if (OsNameEq(s, len, "STEP")) return Tk.Step;
                if (OsNameEq(s, len, "BASE")) return Tk.Base;
                if (OsNameEq(s, len, "ENUM")) return Tk.Enum;
                break;
            case 5:
                if (OsNameEq(s, len, "WHILE")) return Tk.While;
                if (OsNameEq(s, len, "PRINT")) return Tk.Print;
                if (OsNameEq(s, len, "INPUT")) return Tk.Input;
                if (OsNameEq(s, len, "CLEAR")) return Tk.Clear;
                if (OsNameEq(s, len, "FALSE")) return Tk.False;
                if (OsNameEq(s, len, "CLASS")) return Tk.Class;
                if (OsNameEq(s, len, "BREAK")) return Tk.Break;
                if (OsNameEq(s, len, "USING")) return Tk.Using;
                if (OsNameEq(s, len, "EVENT")) return Tk.Event;
                if (OsNameEq(s, len, "CATCH")) return Tk.Catch;
                break;
            case 6:
                if (OsNameEq(s, len, "RETURN")) return Tk.Return;
                if (OsNameEq(s, len, "GLOBAL")) return Tk.Global;
                if (OsNameEq(s, len, "INTERFACE")) return Tk.Interface;
                if (OsNameEq(s, len, "PUBLIC")) return Tk.Public;
                break;
            case 7:
                if (OsNameEq(s, len, "PRIVATE")) return Tk.Private;
                break;
            case 8:
                if (OsNameEq(s, len, "CONTINUE")) return Tk.Continue;
                if (OsNameEq(s, len, "FUNCTION")) return Tk.Function;
                break;
            case 9:
                if (OsNameEq(s, len, "PROTECTED")) return Tk.Protected;
                break;
            case 11:
                if (OsNameEq(s, len, "CONSTRUCTOR")) return Tk.Constructor;
                break;
        }
        return Tk.Id;
    }

    // ==================================================================
    //  PARSER
    // ==================================================================
    private static bool OsAtEnd() => _osToks[_osPos].K == Tk.Eof;
    private static Tk OsCur() => _osToks[_osPos].K;
    private static Tk OsPeek() => _osToks[_osPos + 1].K;
    private static bool OsIsStatementStart(Tk k) => k switch
    {
        Tk.Print or Tk.Show or Tk.Input or Tk.Clear or
        Tk.If or Tk.While or Tk.For or Tk.Function or Tk.Return or
        Tk.Break or Tk.Continue or Tk.Var or Tk.Global or Tk.Class or
        Tk.Enum or Tk.Using or Tk.Try or Tk.Event => true,
        _ => false
    };
    private static Tok* OsTakeName(string msg)
    {
        if (_osToks[_osPos].K == Tk.Id) return OsAdvance();
        OsFail(msg);
        return &_osToks[_osPos];
    }
    private static void OsFailNeedsFunction(char* name, int nlen)
    {
        OsFail("Expected a function value.");
    }
    private static bool OsCheck(Tk k) => _osToks[_osPos].K == k;
    private static Tok* OsAdvance()
    {
        if (_osToks[_osPos].K != Tk.Eof) _osPos++;
        return &_osToks[_osPos - 1];
    }
    private static Tok* OsExpect(Tk k, string msg)
    {
        if (_osToks[_osPos].K != k) { OsFail(msg); return &_osToks[_osPos]; }
        if (_osToks[_osPos].K != Tk.Eof) _osPos++;
        return &_osToks[_osPos - 1];
    }
    private static void OsSkipNewlines() { while (_osToks[_osPos].K == Tk.Newline) _osPos++; }
    private static bool OsIsTerm(int k, int a, int b, int c) => k == a || k == b || k == c;

    private static Expr* OsMakeId(char* name, int len)
    {
        Expr* e = OsAllocExpr();
        e->K = Ek.Id; e->N = name; e->NL = len;
        return e;
    }

    private static Stmt* OsParseProgram(out int topCount)
    {
        int cap = 16;
        Stmt* top = (Stmt*)OsAllocStmtArr(cap);
        topCount = 0;
        OsSkipNewlines();
        while (!OsAtEnd())
        {
            Tk k = OsCur();
            if (k == Tk.Function)
            {
                OsParseFunctionDecl();
            }
            else if (k == Tk.Class)
            {
                OsParseClassDecl();
            }
            else if (k == Tk.Enum)
            {
                OsParseEnumDecl();
            }
            else if (k == Tk.Using)
            {
                OsAdvance(); // USING - read names until newline, ignore
                while (!OsAtEnd() && OsCur() != Tk.Newline) OsAdvance();
            }
            else if (k == Tk.Interface)
            {
                OsParseInterfaceDecl();
            }
            else if (k == Tk.Event)
            {
                OsFail("Events are not supported in this OSLANG subset.");
                return top;
            }
            else
            {
                Stmt s = OsParseStatement();
                if (_osErr) return top;
                if (topCount >= cap)
                {
                    int nc = cap * 2;
                    Stmt* nt = (Stmt*)OsAllocStmtArr(nc);
                    for (int i = 0; i < topCount; i++) nt[i] = top[i];
                    top = nt; cap = nc;
                }
                top[topCount++] = s;
            }
            OsSkipNewlines();
        }
        return top;
    }

    private static void OsParseFunctionDecl()
    {
        OsAdvance(); // FUNCTION
        Tok* nameTok = OsExpect(Tk.Id, "Expected function name.");
        if (_osErr) return;
        OsExpect(Tk.LParen, "Expected '(' after function name.");
        if (_osErr) return;
        int pcap = 8;
        char** pnames = (char**)OsAlloc(pcap * sizeof(char*));
        int* plens = (int*)OsAlloc(pcap * sizeof(int));
        int pc = 0;
        while (!OsCheck(Tk.RParen) && !OsAtEnd())
        {
            Tok* pt = OsExpect(Tk.Id, "Expected parameter name.");
            if (_osErr) return;
            if (pc >= pcap)
            {
                int nc = pcap * 2;
                char** np = (char**)OsAlloc(nc * sizeof(char*));
                int* nl = (int*)OsAlloc(nc * sizeof(int));
                for (int i = 0; i < pc; i++) { np[i] = pnames[i]; nl[i] = plens[i]; }
                pnames = np; plens = nl; pcap = nc;
            }
            pnames[pc] = pt->T;
            plens[pc] = pt->L;
            pc++;
            if (OsCheck(Tk.Comma)) { OsAdvance(); }
            else break;
        }
        OsExpect(Tk.RParen, "Expected ')' after parameters.");
        if (_osErr) return;
        OsSkipNewlines();
        Stmt* body; int bl;
        OsParseBlock(out body, out bl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
        if (_osErr) return;
        OsExpect(Tk.End, "Expected END to close FUNCTION.");
        if (OsCheck(Tk.Function)) OsAdvance(); // optional END FUNCTION
        if (_osFuncCount >= OS_MAX_FUNCS) { OsFail("Too many functions."); return; }
        FuncDef* f = &_osFuncs[_osFuncCount++];
        f->N = nameTok->T; f->NL = nameTok->L;
        f->P = pnames; f->PL = plens; f->PC = pc;
        f->Body = body; f->BL = bl;
    }

    private static void OsParseClassDecl()
    {
        OsAdvance(); // CLASS
        Tok* nameTok = OsExpect(Tk.Id, "Expected class name.");
        if (_osErr) return;

        int parentClassId = -1;
        if (OsCheck(Tk.Colon))
        {
            OsAdvance(); // :
            Tok* parentTok = OsExpect(Tk.Id, "Expected parent class name.");
            if (_osErr) return;
            for (int i = 0; i < _osClassCount; i++)
            {
                if (_osClasses[i].NL == parentTok->L && OsNameCI(_osClasses[i].N, _osClasses[i].NL, parentTok->T, parentTok->L))
                {
                    parentClassId = i;
                    break;
                }
            }
            if (parentClassId == -1) { OsFail("Parent class not found."); return; }
        }

        OsSkipNewlines();

        // Parse class body: fields (PUBLIC/PRIVATE/PROTECTED ID) and methods (ID(params)) and constructor (CONSTRUCTOR)
        int fieldCap = 8;
        char** fields = (char**)OsAlloc(fieldCap * sizeof(char*));
        int* fieldLens = (int*)OsAlloc(fieldCap * sizeof(int));
        int fieldCount = 0;

        int methodCap = 8;
        char** methods = (char**)OsAlloc(methodCap * sizeof(char*));
        Stmt** methodBodies = (Stmt**)OsAlloc(methodCap * sizeof(Stmt*));
        int* methodBodyLens = (int*)OsAlloc(methodCap * sizeof(int));
        // Method parameters
        char*** methodParamNames = (char***)OsAlloc(methodCap * sizeof(char**));
        int** methodParamLens = (int**)OsAlloc(methodCap * sizeof(int*));
        int* methodParamCounts = (int*)OsAlloc(methodCap * sizeof(int));
        int methodCount = 0;

        // Constructor
        Stmt* constructorBody = null;
        int constructorBodyLen = 0;

        while (!OsAtEnd() && !OsCheck(Tk.End))
        {
            OsSkipNewlines();
            if (OsAtEnd() || OsCheck(Tk.End)) break;

            Tk k = OsCur();
            if (k == Tk.Public || k == Tk.Private || k == Tk.Protected)
            {
                // Field declaration with visibility
                OsAdvance(); // visibility modifier
                Tok* fieldTok = OsExpect(Tk.Id, "Expected field name.");
                if (_osErr) return;
                if (fieldCount >= fieldCap)
                {
                    int nc = fieldCap * 2;
                    char** nf = (char**)OsAlloc(nc * sizeof(char*));
                    int* nl = (int*)OsAlloc(nc * sizeof(int));
                    for (int i = 0; i < fieldCount; i++) { nf[i] = fields[i]; nl[i] = fieldLens[i]; }
                    fields = nf; fieldLens = nl; fieldCap = nc;
                }
                fields[fieldCount] = fieldTok->T;
                fieldLens[fieldCount] = fieldTok->L;
                fieldCount++;
            }
            else if (k == Tk.Constructor)
            {
                // Constructor declaration
                OsAdvance(); // CONSTRUCTOR
                OsExpect(Tk.LParen, "Expected '(' after CONSTRUCTOR.");
                if (_osErr) return;
                int pcap = 8;
                char** pnames = (char**)OsAlloc(pcap * sizeof(char*));
                int* plens = (int*)OsAlloc(pcap * sizeof(int));
                int pc = 0;
                while (!OsCheck(Tk.RParen) && !OsAtEnd())
                {
                    Tok* pt = OsExpect(Tk.Id, "Expected parameter name.");
                    if (_osErr) return;
                    if (pc >= pcap)
                    {
                        int nc = pcap * 2;
                        char** np = (char**)OsAlloc(nc * sizeof(char*));
                        int* nl = (int*)OsAlloc(nc * sizeof(int));
                        for (int i = 0; i < pc; i++) { np[i] = pnames[i]; nl[i] = plens[i]; }
                        pnames = np; plens = nl; pcap = nc;
                    }
                    pnames[pc] = pt->T;
                    plens[pc] = pt->L;
                    pc++;
                    if (OsCheck(Tk.Comma)) { OsAdvance(); }
                    else break;
                }
                OsExpect(Tk.RParen, "Expected ')' after parameters.");
                if (_osErr) return;
                OsSkipNewlines();
                Stmt* cbody; int cbl;
                OsParseBlock(out cbody, out cbl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
                if (_osErr) return;
                OsExpect(Tk.End, "Expected END to close CONSTRUCTOR.");
                if (OsCheck(Tk.Constructor)) OsAdvance();
                
                constructorBody = cbody;
                constructorBodyLen = cbl;
                // Store constructor as a special method
                if (methodCount >= methodCap)
                {
                    int nc = methodCap * 2;
                    char** nm = (char**)OsAlloc(nc * sizeof(char*));
                    Stmt** nb = (Stmt**)OsAlloc(nc * sizeof(Stmt*));
                    int* nbl = (int*)OsAlloc(nc * sizeof(int));
                    char*** npm = (char***)OsAlloc(nc * sizeof(char**));
                    int** npl = (int**)OsAlloc(nc * sizeof(int*));
                    int* npc = (int*)OsAlloc(nc * sizeof(int));
                    for (int i = 0; i < methodCount; i++) { nm[i] = methods[i]; nb[i] = methodBodies[i]; nbl[i] = methodBodyLens[i]; npm[i] = methodParamNames[i]; npl[i] = methodParamLens[i]; npc[i] = methodParamCounts[i]; }
                    methods = nm; methodBodies = nb; methodBodyLens = nbl; methodParamNames = npm; methodParamLens = npl; methodParamCounts = npc; methodCap = nc;
                }
                methods[methodCount] = (char*)OsAlloc(12 * 2); // "CONSTRUCTOR" + null
                fixed (char* ctorName = "CONSTRUCTOR")
                {
                    for (int i = 0; i < 11; i++) methods[methodCount][i] = ctorName[i];
                    methods[methodCount][11] = '\0';
                }
                methodBodies[methodCount] = cbody;
                methodBodyLens[methodCount] = cbl;
                // Store constructor parameters
                methodParamCounts[methodCount] = pc;
                if (pc > 0)
                {
                    char** mparams = (char**)OsAlloc(pc * sizeof(char*));
                    int* mplens = (int*)OsAlloc(pc * sizeof(int));
                    for (int i = 0; i < pc; i++)
                    {
                        mparams[i] = (char*)OsAlloc((plens[i] + 1) * sizeof(char));
                        for (int j = 0; j < plens[i]; j++) mparams[i][j] = pnames[i][j];
                        mparams[i][plens[i]] = '\0';
                        mplens[i] = plens[i];
                    }
                    methodParamNames[methodCount] = mparams;
                    methodParamLens[methodCount] = mplens;
                }
                else
                {
                    methodParamNames[methodCount] = null;
                    methodParamLens[methodCount] = null;
                }
                methodCount++;
            }
            else if (k == Tk.Function)
            {
                // Method declaration with optional FUNCTION keyword
                OsAdvance(); // FUNCTION
                Tok* methodTok = OsExpect(Tk.Id, "Expected method name.");
                if (_osErr) return;
                OsExpect(Tk.LParen, "Expected '(' after method name.");
                if (_osErr) return;
                int pcap = 8;
                char** pnames = (char**)OsAlloc(pcap * sizeof(char*));
                int* plens = (int*)OsAlloc(pcap * sizeof(int));
                int pc = 0;
                while (!OsCheck(Tk.RParen) && !OsAtEnd())
                {
                    Tok* pt = OsExpect(Tk.Id, "Expected parameter name.");
                    if (_osErr) return;
                    if (pc >= pcap)
                    {
                        int nc = pcap * 2;
                        char** np = (char**)OsAlloc(nc * sizeof(char*));
                        int* nl = (int*)OsAlloc(nc * sizeof(int));
                        for (int i = 0; i < pc; i++) { np[i] = pnames[i]; nl[i] = plens[i]; }
                        pnames = np; plens = nl; pcap = nc;
                    }
                    pnames[pc] = pt->T;
                    plens[pc] = pt->L;
                    pc++;
                    if (OsCheck(Tk.Comma)) { OsAdvance(); }
                    else break;
                }
                OsExpect(Tk.RParen, "Expected ')' after parameters.");
                if (_osErr) return;
                OsSkipNewlines();
                Stmt* mbody; int mbl;
                OsParseBlock(out mbody, out mbl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
                if (_osErr) return;
                OsExpect(Tk.End, "Expected END to close FUNCTION.");
                if (OsCheck(Tk.Function)) OsAdvance();

                if (methodCount >= methodCap)
                {
                    int nc = methodCap * 2;
                    char** nm = (char**)OsAlloc(nc * sizeof(char*));
                    Stmt** nb = (Stmt**)OsAlloc(nc * sizeof(Stmt*));
                    int* nbl = (int*)OsAlloc(nc * sizeof(int));
                    char*** npm = (char***)OsAlloc(nc * sizeof(char**));
                    int** npl = (int**)OsAlloc(nc * sizeof(int*));
                    int* npc = (int*)OsAlloc(nc * sizeof(int));
                    for (int i = 0; i < methodCount; i++) { nm[i] = methods[i]; nb[i] = methodBodies[i]; nbl[i] = methodBodyLens[i]; npm[i] = methodParamNames[i]; npl[i] = methodParamLens[i]; npc[i] = methodParamCounts[i]; }
                    methods = nm; methodBodies = nb; methodBodyLens = nbl; methodParamNames = npm; methodParamLens = npl; methodParamCounts = npc; methodCap = nc;
                }
                int methodNameLen = methodTok->L;
                methods[methodCount] = (char*)OsAlloc((methodNameLen + 1) * sizeof(char));
                for (int i = 0; i < methodNameLen; i++) methods[methodCount][i] = methodTok->T[i];
                methods[methodCount][methodNameLen] = '\0';
                methodBodies[methodCount] = mbody;
                methodBodyLens[methodCount] = mbl;
                // Store method parameters
                methodParamCounts[methodCount] = pc;
                if (pc > 0)
                {
                    char** mparams = (char**)OsAlloc(pc * sizeof(char*));
                    int* mplens = (int*)OsAlloc(pc * sizeof(int));
                    for (int i = 0; i < pc; i++)
                    {
                        mparams[i] = (char*)OsAlloc((plens[i] + 1) * sizeof(char));
                        for (int j = 0; j < plens[i]; j++) mparams[i][j] = pnames[i][j];
                        mparams[i][plens[i]] = '\0';
                        mplens[i] = plens[i];
                    }
                    methodParamNames[methodCount] = mparams;
                    methodParamLens[methodCount] = mplens;
                }
                else
                {
                    methodParamNames[methodCount] = null;
                    methodParamLens[methodCount] = null;
                }
                methodCount++;
            }
            else if (k == Tk.Id)
            {
                // Method declaration (no FUNCTION keyword per OSLANG 0.2)
                // Check if followed by LParen (method) or just a field without visibility
                if (OsPeek() == Tk.LParen)
                {
                    Tok* methodTok = OsExpect(Tk.Id, "Expected method name.");
                    OsAdvance(); // LParen
                    int pcap = 8;
                    char** pnames = (char**)OsAlloc(pcap * sizeof(char*));
                    int* plens = (int*)OsAlloc(pcap * sizeof(int));
                    int pc = 0;
                    while (!OsCheck(Tk.RParen) && !OsAtEnd())
                    {
                        Tok* pt = OsExpect(Tk.Id, "Expected parameter name.");
                        if (_osErr) return;
                        if (pc >= pcap)
                        {
                            int nc = pcap * 2;
                            char** np = (char**)OsAlloc(nc * sizeof(char*));
                            int* nl = (int*)OsAlloc(nc * sizeof(int));
                            for (int i = 0; i < pc; i++) { np[i] = pnames[i]; nl[i] = plens[i]; }
                            pnames = np; plens = nl; pcap = nc;
                        }
                        pnames[pc] = pt->T;
                        plens[pc] = pt->L;
                        pc++;
                        if (OsCheck(Tk.Comma)) { OsAdvance(); }
                        else break;
                    }
                    OsExpect(Tk.RParen, "Expected ')' after parameters.");
                    if (_osErr) return;
                    OsSkipNewlines();
                    Stmt* mbody; int mbl;
                    OsParseBlock(out mbody, out mbl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
                    if (_osErr) return;
                    OsExpect(Tk.End, "Expected END to close method.");
                    if (OsCheck(Tk.Function)) OsAdvance(); // optional FUNCTION keyword

                    if (methodCount >= methodCap)
                    {
                        int nc = methodCap * 2;
                        char** nm = (char**)OsAlloc(nc * sizeof(char*));
                        Stmt** nb = (Stmt**)OsAlloc(nc * sizeof(Stmt*));
                        int* nbl = (int*)OsAlloc(nc * sizeof(int));
                        char*** npm = (char***)OsAlloc(nc * sizeof(char**));
                        int** npl = (int**)OsAlloc(nc * sizeof(int*));
                        int* npc = (int*)OsAlloc(nc * sizeof(int));
                        for (int i = 0; i < methodCount; i++) { nm[i] = methods[i]; nb[i] = methodBodies[i]; nbl[i] = methodBodyLens[i]; npm[i] = methodParamNames[i]; npl[i] = methodParamLens[i]; npc[i] = methodParamCounts[i]; }
                        methods = nm; methodBodies = nb; methodBodyLens = nbl; methodParamNames = npm; methodParamLens = npl; methodParamCounts = npc; methodCap = nc;
                    }
                    int methodNameLen = methodTok->L;
                    methods[methodCount] = (char*)OsAlloc((methodNameLen + 1) * sizeof(char));
                    for (int i = 0; i < methodNameLen; i++) methods[methodCount][i] = methodTok->T[i];
                    methods[methodCount][methodNameLen] = '\0';
                    methodBodies[methodCount] = mbody;
                    methodBodyLens[methodCount] = mbl;
                    // Store method parameters
                    methodParamCounts[methodCount] = pc;
                    if (pc > 0)
                    {
                        char** mparams = (char**)OsAlloc(pc * sizeof(char*));
                        int* mplens = (int*)OsAlloc(pc * sizeof(int));
                        for (int i = 0; i < pc; i++)
                        {
                            mparams[i] = (char*)OsAlloc((plens[i] + 1) * sizeof(char));
                            for (int j = 0; j < plens[i]; j++) mparams[i][j] = pnames[i][j];
                            mparams[i][plens[i]] = '\0';
                            mplens[i] = plens[i];
                        }
                        methodParamNames[methodCount] = mparams;
                        methodParamLens[methodCount] = mplens;
                    }
                    else
                    {
                        methodParamNames[methodCount] = null;
                        methodParamLens[methodCount] = null;
                    }
                    methodCount++;
                }
                else
                {
                    // Field without visibility modifier (default PUBLIC)
                    Tok* fieldTok = OsExpect(Tk.Id, "Expected field name.");
                    if (fieldCount >= fieldCap)
                    {
                        int nc = fieldCap * 2;
                        char** nf = (char**)OsAlloc(nc * sizeof(char*));
                        int* nl = (int*)OsAlloc(nc * sizeof(int));
                        for (int i = 0; i < fieldCount; i++) { nf[i] = fields[i]; nl[i] = fieldLens[i]; }
                        fields = nf; fieldLens = nl; fieldCap = nc;
                    }
                    fields[fieldCount] = fieldTok->T;
                    fieldLens[fieldCount] = fieldTok->L;
                    fieldCount++;
                }
            }
            else
            {
                OsFail("Expected field, method, or constructor in class body.");
                return;
            }
        }

        OsExpect(Tk.End, "Expected END to close CLASS.");
        OsSkipNewlines();
        if (OsCheck(Tk.Class)) OsAdvance();

        if (_osClassCount >= OS_MAX_CLASSES) { OsFail("Too many classes."); return; }
        ClassDef* c = &_osClasses[_osClassCount++];
        c->N = nameTok->T; c->NL = nameTok->L;
        c->Fields = fields; c->FieldLens = fieldLens; c->FieldCount = fieldCount;
        c->Methods = methods; c->MethodBodies = methodBodies; c->MethodBodyLens = methodBodyLens; c->MethodCount = methodCount;
        c->MethodParamNames = (char***)OsAlloc(methodCount * sizeof(char**));
        c->MethodParamLens = (int**)OsAlloc(methodCount * sizeof(int*));
        c->MethodParamCounts = (int*)OsAlloc(methodCount * sizeof(int));
        for (int i = 0; i < methodCount; i++)
        {
            c->MethodParamNames[i] = methodParamNames[i];
            c->MethodParamLens[i] = methodParamLens[i];
            c->MethodParamCounts[i] = methodParamCounts[i];
        }
        c->ParentClassId = parentClassId;
    }

    private static void OsParseEnumDecl()
    {
        OsAdvance(); // ENUM
        Tok* nameTok = OsExpect(Tk.Id, "Expected enum name.");
        if (_osErr) return;

        // Check for FLAGS modifier
        bool isFlags = false;
        if (OsCheck(Tk.Id))
        {
            // Peek ahead to see if it's FLAGS
            // For simplicity, we'll check if the next identifier is FLAGS
        }

        OsSkipNewlines();

        int memberCap = 8;
        char** members = (char**)OsAlloc(memberCap * sizeof(char*));
        int* memberLens = (int*)OsAlloc(memberCap * sizeof(int));
        ulong* values = (ulong*)OsAlloc(memberCap * sizeof(ulong));
        int memberCount = 0;
        ulong nextValue = 0;

        while (!OsAtEnd() && !OsCheck(Tk.End))
        {
            OsSkipNewlines();
            if (OsAtEnd() || OsCheck(Tk.End)) break;

            Tok* memberTok = OsExpect(Tk.Id, "Expected enum member name.");
            if (_osErr) return;

            ulong value = nextValue;
            if (OsCheck(Tk.Equal))
            {
                OsAdvance(); // =
                Tok* valTok = OsExpect(Tk.Num, "Expected numeric value for enum member.");
                if (_osErr) return;
                value = (ulong)valTok->N;
                nextValue = value + 1;
            }
            else
            {
                nextValue = value + 1;
            }

            if (memberCount >= memberCap)
            {
                int nc = memberCap * 2;
                char** nm = (char**)OsAlloc(nc * sizeof(char*));
                int* nl = (int*)OsAlloc(nc * sizeof(int));
                ulong* nv = (ulong*)OsAlloc(nc * sizeof(ulong));
                for (int i = 0; i < memberCount; i++) { nm[i] = members[i]; nl[i] = memberLens[i]; nv[i] = values[i]; }
                members = nm; memberLens = nl; values = nv; memberCap = nc;
            }
            members[memberCount] = memberTok->T;
            memberLens[memberCount] = memberTok->L;
            values[memberCount] = value;
            memberCount++;

            if (OsCheck(Tk.Comma)) { OsAdvance(); }
        }

        OsExpect(Tk.End, "Expected END to close ENUM.");
        OsSkipNewlines();
        if (OsCheck(Tk.Enum)) OsAdvance();

        if (_osEnumCount >= OS_MAX_ENUMS) { OsFail("Too many enums."); return; }
        EnumDef* e = &_osEnums[_osEnumCount++];
        e->N = nameTok->T; e->NL = nameTok->L;
        e->Members = members; e->MemberLens = memberLens; e->Values = values;
        e->MemberCount = memberCount;
        e->IsFlags = isFlags;
    }

    private static void OsParseInterfaceDecl()
    {
        OsAdvance(); // INTERFACE
        Tok* nameTok = OsExpect(Tk.Id, "Expected interface name.");
        if (_osErr) return;

        OsSkipNewlines();

        int memberCap = 8;
        char** members = (char**)OsAlloc(memberCap * sizeof(char*));
        int* memberLens = (int*)OsAlloc(memberCap * sizeof(int));
        int memberCount = 0;

        while (!OsAtEnd() && !OsCheck(Tk.End))
        {
            OsSkipNewlines();
            if (OsAtEnd() || OsCheck(Tk.End)) break;

            Tk k = OsCur();
            if (k == Tk.Var)
            {
                // Property declaration in interface
                OsAdvance(); // VAR
                Tok* memberTok = OsExpect(Tk.Id, "Expected property name.");
                if (_osErr) return;
                if (memberCount >= memberCap)
                {
                    int nc = memberCap * 2;
                    char** nm = (char**)OsAlloc(nc * sizeof(char*));
                    int* nl = (int*)OsAlloc(nc * sizeof(int));
                    for (int i = 0; i < memberCount; i++) { nm[i] = members[i]; nl[i] = memberLens[i]; }
                    members = nm; memberLens = nl; memberCap = nc;
                }
                members[memberCount] = memberTok->T;
                memberLens[memberCount] = memberTok->L;
                memberCount++;
            }
            else if (k == Tk.Id)
            {
                // Method declaration in interface (no FUNCTION keyword)
                // Check if followed by LParen
                if (OsPeek() == Tk.LParen)
                {
                    Tok* methodTok = OsExpect(Tk.Id, "Expected method name.");
                    OsAdvance(); // LParen
                    // Skip parameters for interface
                    while (!OsCheck(Tk.RParen) && !OsAtEnd())
                    {
                        if (OsCur() == Tk.Id) OsAdvance();
                        if (OsCheck(Tk.Comma)) OsAdvance();
                        else break;
                    }
                    OsExpect(Tk.RParen, "Expected ')' after parameters.");
                    if (_osErr) return;
                    
                    if (memberCount >= memberCap)
                    {
                        int nc = memberCap * 2;
                        char** nm = (char**)OsAlloc(nc * sizeof(char*));
                        int* nl = (int*)OsAlloc(nc * sizeof(int));
                        for (int i = 0; i < memberCount; i++) { nm[i] = members[i]; nl[i] = memberLens[i]; }
                        members = nm; memberLens = nl; memberCap = nc;
                    }
                    members[memberCount] = methodTok->T;
                    memberLens[memberCount] = methodTok->L;
                    memberCount++;
                }
                else
                {
                    OsFail("Expected method with parameters or property in interface.");
                    return;
                }
            }
            else
            {
                OsFail("Expected property or method in interface body.");
                return;
            }
        }

        OsExpect(Tk.End, "Expected END to close INTERFACE.");
        OsSkipNewlines();
        if (OsCheck(Tk.Interface)) OsAdvance();

        if (_osInterfaceCount >= OS_MAX_INTERFACES) { OsFail("Too many interfaces."); return; }
        InterfaceDef* iface = &_osInterfaces[_osInterfaceCount++];
        iface->N = nameTok->T; iface->NL = nameTok->L;
        iface->Members = members; iface->MemberLens = memberLens; iface->MemberCount = memberCount;
    }

    private static void OsParseBlock(out Stmt* body, out int count, int t1, int t2, int t3)
    {
        int cap = 16;
        Stmt* arr = (Stmt*)OsAllocStmtArr(cap);
        count = 0;
        body = arr;
        while (true)
        {
            OsSkipNewlines();
            if (OsAtEnd()) break;
            Tk k = OsCur();
            if (OsIsTerm((int)k, t1, t2, t3)) break;
            Stmt s = OsParseStatement();
            if (_osErr) return;
            if (count >= cap)
            {
                int nc = cap * 2;
                Stmt* na = (Stmt*)OsAllocStmtArr(nc);
                for (int i = 0; i < count; i++) na[i] = arr[i];
                arr = na; cap = nc;
            }
            arr[count++] = s;
        }
        body = arr;
    }

    private static Stmt OsParseStatement()
    {
        Tk k = OsCur();
        switch (k)
        {
            case Tk.Print: return OsParsePrintShow(Sk.Print);
            case Tk.Show: return OsParsePrintShow(Sk.Show);
            case Tk.Input: return OsParseInput();
            case Tk.Clear: { OsAdvance(); Stmt s = default; s.K = Sk.Clear; return s; }
            case Tk.If: return OsParseIf();
            case Tk.While: return OsParseWhile();
            case Tk.For: return OsParseFor();
            case Tk.Do: return OsParseDoWhile();
            case Tk.Try: return OsParseTryCatch();
            case Tk.Return: return OsParseReturn();
            case Tk.Var: return OsParseVar(Sk.Var);
            case Tk.Global: return OsParseVar(Sk.Global);
            case Tk.Break: { OsAdvance(); Stmt s = default; s.K = Sk.Break; return s; }
            case Tk.Continue: { OsAdvance(); Stmt s = default; s.K = Sk.Continue; return s; }
            default: return OsParseExprLed();
        }
    }

    private static Stmt OsParsePrintShow(Sk kind)
    {
        OsAdvance(); // PRINT/SHOW
        Stmt s = default;
        s.K = kind;
        int cap = 4;
        Expr** args = (Expr**)OsAllocExprArr(cap);
        int ac = 0;
        if (!OsCheck(Tk.Newline) && !OsAtEnd())
        {
            args[ac++] = OsParseExpr();
            if (_osErr) { s.Args = args; s.AC = ac; return s; }
            while (OsCheck(Tk.Comma))
            {
                OsAdvance();
                if (ac >= cap)
                {
                    int nc = cap * 2;
                    Expr** na = (Expr**)OsAllocExprArr(nc);
                    for (int i = 0; i < ac; i++) na[i] = args[i];
                    args = na; cap = nc;
                }
                args[ac++] = OsParseExpr();
                if (_osErr) { s.Args = args; s.AC = ac; return s; }
            }
        }
        s.Args = args;
        s.AC = ac;
        return s;
    }

    private static Stmt OsParseInput()
    {
        OsAdvance(); // INPUT
        Tok* name = OsExpect(Tk.Id, "Expected variable name after INPUT.");
        Stmt s = default;
        s.K = Sk.Input;
        s.N = name->T; s.NL = name->L;
        return s;
    }

    private static Stmt OsParseIf()
    {
        OsAdvance(); // IF or ELIF
        Expr* cond = OsParseExpr();
        if (_osErr) return default;
        OsExpect(Tk.Then, "Expected THEN after IF condition.");
        if (_osErr) return default;
        Stmt* thenBody; int thenLen;
        OsParseBlock(out thenBody, out thenLen, (int)Tk.Elif, (int)Tk.Else, (int)Tk.End);
        if (_osErr) return default;
        Stmt* elseBody = null; int elseLen = 0;
        if (OsCheck(Tk.Elif))
        {
            Stmt elif = OsParseIf(); // recursively parse the ELIF chain
            if (_osErr) return default;
            elseBody = (Stmt*)OsAllocStmtArr(1);
            elseBody[0] = elif;
            elseLen = 1;
        }
        else if (OsCheck(Tk.Else))
        {
            OsAdvance(); // ELSE
            OsParseBlock(out elseBody, out elseLen, (int)Tk.End, (int)Tk.None, (int)Tk.None);
            if (_osErr) return default;
            OsExpect(Tk.End, "Expected END to close IF.");
            if (OsCheck(Tk.If)) OsAdvance();
        }
        else
        {
            OsExpect(Tk.End, "Expected END to close IF.");
            if (OsCheck(Tk.If)) OsAdvance();
        }

        Stmt s = default;
        s.K = Sk.If;
        s.E1 = cond;
        s.Body = thenBody; s.BL = thenLen;
        s.ElseBody = elseBody; s.EL = elseLen;
        return s;
    }

    private static Stmt OsParseWhile()
    {
        OsAdvance(); // WHILE
        Expr* cond = OsParseExpr();
        if (_osErr) return default;
        Stmt* body; int bl;
        OsParseBlock(out body, out bl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
        if (_osErr) return default;
        OsExpect(Tk.End, "Expected END to close WHILE.");
        Stmt s = default;
        s.K = Sk.While;
        s.E1 = cond;
        s.Body = body; s.BL = bl;
        return s;
    }

    private static Stmt OsParseDoWhile()
    {
        OsAdvance(); // DO
        Stmt* body; int bl;
        OsParseBlock(out body, out bl, (int)Tk.While, (int)Tk.None, (int)Tk.None);
        if (_osErr) return default;
        OsExpect(Tk.While, "Expected WHILE after DO block.");
        if (_osErr) return default;
        Expr* cond = OsParseExpr();
        if (_osErr) return default;
        Stmt s = default;
        s.K = Sk.DoWhile;
        s.E1 = cond;
        s.Body = body; s.BL = bl;
        return s;
    }

    private static Stmt OsParseTryCatch()
    {
        OsAdvance(); // TRY
        OsSkipNewlines();
        Stmt* tryBody; int tryBl;
        OsParseBlock(out tryBody, out tryBl, (int)Tk.Catch, (int)Tk.End, (int)Tk.None);
        if (_osErr) return default;
        
        // Expect CATCH
        OsExpect(Tk.Catch, "Expected CATCH after TRY block.");
        if (_osErr) return default;
        Tok* errVar = OsExpect(Tk.Id, "Expected error variable name after CATCH.");
        if (_osErr) return default;
        
        OsSkipNewlines();
        Stmt* catchBody; int catchBl;
        OsParseBlock(out catchBody, out catchBl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
        if (_osErr) return default;
        OsExpect(Tk.End, "Expected END to close CATCH.");
        
        Stmt s = default;
        s.K = Sk.TryCatch;
        s.N = errVar->T; s.NL = errVar->L;
        s.Body = tryBody; s.BL = tryBl;
        s.ElseBody = catchBody; s.EL = catchBl;
        return s;
    }

    private static Stmt OsParseFor()
    {
        OsAdvance(); // FOR
        Tok* varName = OsExpect(Tk.Id, "Expected loop variable after FOR.");
        if (_osErr) return default;
        OsExpect(Tk.Equal, "Expected '=' after FOR variable.");
        if (_osErr) return default;
        Expr* from = OsParseExpr();
        if (_osErr) return default;
        OsExpect(Tk.To, "Expected TO in FOR.");
        if (_osErr) return default;
        Expr* to = OsParseExpr();
        if (_osErr) return default;
        Expr* step = null;
        if (OsCheck(Tk.Step))
        {
            OsAdvance();
            step = OsParseExpr();
            if (_osErr) return default;
        }
        Stmt* body; int bl;
        OsParseBlock(out body, out bl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
        if (_osErr) return default;
        OsExpect(Tk.End, "Expected END to close FOR.");
        Stmt s = default;
        s.K = Sk.For;
        s.N = varName->T; s.NL = varName->L;
        s.E1 = from; s.E2 = to; s.E3 = step;
        s.Body = body; s.BL = bl;
        return s;
    }

    private static Stmt OsParseReturn()
    {
        OsAdvance(); // RETURN
        Stmt s = default;
        s.K = Sk.Return;
        if (OsCheck(Tk.Newline) || OsAtEnd() || OsCheck(Tk.End) || OsCheck(Tk.Elif) || OsCheck(Tk.Else))
        {
            s.E1 = null;
            return s;
        }
        s.E1 = OsParseExpr();
        return s;
    }

    private static Stmt OsParseVar(Sk kind)
    {
        OsAdvance(); // VAR / GLOBAL
        Tok* name = OsExpect(Tk.Id, "Expected variable name.");
        if (_osErr) return default;
        Stmt s = default;
        s.K = kind;
        s.N = name->T; s.NL = name->L;
        if (OsCheck(Tk.Equal))
        {
            OsAdvance();
            s.E1 = OsParseExpr();
        }
        else
        {
            s.E1 = null;
            if (OsCheck(Tk.Id)) OsAdvance(); // skip optional type annotation
        }
        return s;
    }

    private static Stmt OsParseExprLed()
    {
        Expr* e = OsParsePostfix();
        if (_osErr) return default;
        if (OsCheck(Tk.Equal))
        {
            OsAdvance();
            Expr* val = OsParseExpr();
            if (_osErr) return default;
            Stmt s = default;
            s.K = Sk.Assign; s.E1 = e; s.E2 = val;
            return s;
        }
        if (OsCheck(Tk.PlusEqual))
        {
            OsAdvance();
            Expr* val = OsParseExpr();
            if (_osErr) return default;
            Expr* sum = OsAllocExpr();
            sum->K = Ek.Bin; sum->Op = OP_ADD; sum->A = e; sum->B = val;
            Stmt s = default;
            s.K = Sk.Assign; s.E1 = e; s.E2 = sum;
            return s;
        }
        {
            Stmt s = default;
            s.K = Sk.Expr; s.E1 = e;
            return s;
        }
    }

    // ---- expressions (precedence per spec section 24) ----
    private static Expr* OsParseExpr() => OsParseBor();

    private static Expr* OsParseBor()
    {
        Expr* left = OsParseOr();
        if (_osErr) return left;
        while (OsCheck(Tk.Pipe))
        {
            OsAdvance();
            Expr* right = OsParseOr();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = OP_BOR; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseOr()
    {
        Expr* left = OsParseAnd();
        if (_osErr) return left;
        while (OsCheck(Tk.Or))
        {
            OsAdvance();
            Expr* right = OsParseAnd();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = OP_OR; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseAnd()
    {
        Expr* left = OsParseComparison();
        if (_osErr) return left;
        while (OsCheck(Tk.And))
        {
            OsAdvance();
            Expr* right = OsParseComparison();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = OP_AND; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseComparison()
    {
        Expr* left = OsParseAdditive();
        if (_osErr) return left;
        while (true)
        {
            byte op = 0;
            Tk k = OsCur();
            if (k == Tk.Equal) op = OP_EQ;
            else if (k == Tk.NotEqual) op = OP_NE;
            else if (k == Tk.Less) op = OP_LT;
            else if (k == Tk.Greater) op = OP_GT;
            else if (k == Tk.LessEqual) op = OP_LE;
            else if (k == Tk.GreaterEqual) op = OP_GE;
            else break;
            OsAdvance();
            Expr* right = OsParseAdditive();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = op; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseAdditive()
    {
        Expr* left = OsParseMultiplicative();
        if (_osErr) return left;
        while (OsCheck(Tk.Plus) || OsCheck(Tk.Minus))
        {
            byte op = OsCur() == Tk.Plus ? OP_ADD : OP_SUB;
            OsAdvance();
            Expr* right = OsParseMultiplicative();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = op; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseMultiplicative()
    {
        Expr* left = OsParseExponentiation();
        if (_osErr) return left;
        while (OsCheck(Tk.Star) || OsCheck(Tk.Slash) || OsCheck(Tk.Percent) || OsCheck(Tk.Mod))
        {
            Tk k = OsCur();
            OsAdvance();
            byte op = k == Tk.Star ? OP_MUL : k == Tk.Slash ? OP_DIV : OP_MOD;
            Expr* right = OsParseExponentiation();
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = op; bin->A = left; bin->B = right;
            left = bin;
        }
        return left;
    }

    private static Expr* OsParseExponentiation()
    {
        Expr* left = OsParseUnary();
        if (_osErr) return left;
        if (OsCheck(Tk.StarStar))
        {
            OsAdvance();
            Expr* right = OsParseExponentiation(); // right-associative
            if (_osErr) return left;
            Expr* bin = OsAllocExpr();
            bin->K = Ek.Bin; bin->Op = OP_POW; bin->A = left; bin->B = right;
            return bin;
        }
        return left;
    }

    private static Expr* OsParseUnary()
    {
        if (OsCheck(Tk.Not))
        {
            OsAdvance();
            Expr* operand = OsParseUnary();
            if (_osErr) return null;
            Expr* e = OsAllocExpr();
            e->K = Ek.Un; e->Op = OP_NOT; e->A = operand;
            return e;
        }
        if (OsCheck(Tk.Minus))
        {
            OsAdvance();
            Expr* operand = OsParseUnary();
            if (_osErr) return null;
            Expr* e = OsAllocExpr();
            e->K = Ek.Un; e->Op = OP_NEG; e->A = operand;
            return e;
        }
        return OsParsePostfix();
    }

    private static Expr* OsParsePostfix()
    {
        Expr* e = OsParsePrimary();
        if (_osErr) return e;
        while (true)
        {
            Tk k = OsCur();
            if (k == Tk.Dot)
            {
                OsAdvance();
                Tok* m = OsExpect(Tk.Id, "Expected member name after '.'.");
                if (_osErr) return e;
                Expr* mem = OsAllocExpr();
                mem->K = Ek.Member; mem->A = e; mem->N = m->T; mem->NL = m->L;
                e = mem;
            }
            else if (k == Tk.LBracket)
            {
                OsAdvance();
                Expr* idx = OsParseExpr();
                if (_osErr) return e;
                OsExpect(Tk.RBracket, "Expected ']' after index.");
                if (_osErr) return e;
                Expr* ix = OsAllocExpr();
                ix->K = Ek.Index; ix->A = e; ix->B = idx;
                e = ix;
            }
            else if (k == Tk.LParen)
            {
                OsAdvance();
                Expr** args; int ac;
                OsParseArgList(out args, out ac);
                if (_osErr) return e;
                OsExpect(Tk.RParen, "Expected ')' after arguments.");
                if (_osErr) return e;
                Expr* call = OsAllocExpr();
                call->K = Ek.Call; call->A = e; call->Args = args; call->AC = ac;
                e = call;
            }
            else break;
        }
        return e;
    }

    private static Expr* OsParsePrimary()
    {
        Tk k = OsCur();
        if (k == Tk.Num)
        {
            Tok* t = OsAdvance();
            Expr* e = OsAllocExpr();
            e->K = Ek.Num; e->V = OsNum(t->N);
            return e;
        }
        if (k == Tk.Str)
        {
            Tok* t = OsAdvance();
            return OsParseStringLit(t->T, t->L);
        }
        if (k == Tk.True) { OsAdvance(); Expr* e = OsAllocExpr(); e->K = Ek.Bool; e->V = OsBool(true); return e; }
        if (k == Tk.False) { OsAdvance(); Expr* e = OsAllocExpr(); e->K = Ek.Bool; e->V = OsBool(false); return e; }
        if (k == Tk.Null) { OsAdvance(); Expr* e = OsAllocExpr(); e->K = Ek.Null; return e; }
        if (k == Tk.Id)
        {
            Tok* t = OsAdvance();
            if (OsCheck(Tk.LParen))
            {
                OsAdvance();
                Expr** args; int ac;
                OsParseArgList(out args, out ac);
                if (_osErr) return null;
                OsExpect(Tk.RParen, "Expected ')' after arguments.");
                if (_osErr) return null;
                Expr* call = OsAllocExpr();
                call->K = Ek.Call; call->A = OsMakeId(t->T, t->L); call->Args = args; call->AC = ac;
                return call;
            }
            Expr* e = OsAllocExpr();
            e->K = Ek.Id; e->N = t->T; e->NL = t->L;
            return e;
        }
        if (k == Tk.LParen)
        {
            OsAdvance();
            Expr* e = OsParseExpr();
            if (_osErr) return null;
            OsExpect(Tk.RParen, "Expected ')' after expression.");
            if (_osErr) return null;
            return e;
        }
        if (k == Tk.LBracket)
        {
            OsAdvance();
            Expr** elems; int ac;
            OsParseArgList(out elems, out ac);
            if (_osErr) return null;
            OsExpect(Tk.RBracket, "Expected ']' after array literal.");
            if (_osErr) return null;
            Expr* e = OsAllocExpr();
            e->K = Ek.Arr; e->Args = elems; e->AC = ac;
            return e;
        }
        if (k == Tk.New)
        {
            OsAdvance();
            Tok* classTok = OsExpect(Tk.Id, "Expected class name after NEW.");
            if (_osErr) return null;
            OsExpect(Tk.LParen, "Expected '(' after class name in NEW.");
            if (_osErr) return null;
            Expr** args; int ac;
            OsParseArgList(out args, out ac);
            if (_osErr) return null;
            OsExpect(Tk.RParen, "Expected ')' after constructor arguments.");
            if (_osErr) return null;
            Expr* e = OsAllocExpr();
            e->K = Ek.NewObj; e->N = classTok->T; e->NL = classTok->L; e->Args = args; e->AC = ac;
            return e;
        }
        if (k == Tk.Me)
        {
            OsAdvance();
            Expr* e = OsAllocExpr();
            e->K = Ek.This;
            return e;
        }
        OsFail("Unexpected token in expression.");
        return null;
    }

    private static void OsParseArgList(out Expr** args, out int ac)
    {
        int cap = 4;
        Expr** arr = (Expr**)OsAllocExprArr(cap);
        ac = 0;
        args = arr;
        if (!OsCheck(Tk.RParen) && !OsCheck(Tk.RBracket) && !OsAtEnd())
        {
            arr[ac++] = OsParseExpr();
            if (_osErr) return;
            while (OsCheck(Tk.Comma))
            {
                OsAdvance();
                if (ac >= cap)
                {
                    int nc = cap * 2;
                    Expr** na = (Expr**)OsAllocExprArr(nc);
                    for (int i = 0; i < ac; i++) na[i] = arr[i];
                    arr = na; cap = nc;
                }
                arr[ac++] = OsParseExpr();
                if (_osErr) return;
            }
        }
        args = arr;
    }

    // ---- string literal with ${} interpolation ----
    private static Expr* OsParseStringLit(char* raw, int len)
    {
        int holes = 0;
        for (int k = 0; k + 1 < len; k++)
            if (raw[k] == '$' && raw[k + 1] == '{' && (k == 0 || raw[k - 1] != '\\')) holes++;
        if (holes == 0)
        {
            Expr* plain = OsAllocExpr();
            plain->K = Ek.Str;
            plain->V = OsStr(raw, len);
            return plain;
        }
        Expr** parts = (Expr**)OsAllocExprArr(holes * 2 + 2);
        int pc = 0;
        int i = 0, segStart = 0;
        while (i < len)
        {
            if (raw[i] == '$' && i + 1 < len && raw[i + 1] == '{' && (i == 0 || raw[i - 1] != '\\'))
            {
                if (i > segStart)
                {
                    Expr* lit = OsAllocExpr();
                    lit->K = Ek.Str;
                    lit->V = OsStrUnescape(raw + segStart, i - segStart);
                    parts[pc++] = lit;
                }
                int j = i + 2, depth = 1;
                while (j < len && depth > 0)
                {
                    if (raw[j] == '{') depth++;
                    else if (raw[j] == '}') depth--;
                    if (depth == 0) break;
                    j++;
                }
                if (depth != 0) { OsFail("Unterminated string interpolation."); return null; }
                Expr* sub = OsParseSubExpr(raw + i + 2, j - (i + 2));
                if (_osErr) return null;
                parts[pc++] = sub;
                i = j + 1;
                segStart = i;
            }
            else i++;
        }
        if (segStart < len)
        {
            Expr* lit = OsAllocExpr();
            lit->K = Ek.Str;
            lit->V = OsStrUnescape(raw + segStart, len - segStart);
            parts[pc++] = lit;
        }
        Expr* e = OsAllocExpr();
        e->K = Ek.Interp; e->Args = parts; e->AC = pc;
        return e;
    }

    // In processed string content, "\$" is an escaped dollar -> "$".
    private static OslVal OsStrUnescape(char* s, int len)
    {
        char* buf = (char*)OsAlloc(len * 2 + 2);
        int p = 0, i = 0;
        while (i < len)
        {
            if (s[i] == '\\' && i + 1 < len && s[i + 1] == '$') { buf[p++] = '$'; i += 2; }
            else { buf[p++] = s[i++]; }
        }
        OslVal v = default;
        v.K = VKStr; v.P = (ulong)buf; v.L = p;
        return v;
    }

    // Parse an expression from a substring (for interpolation holes).
    private static Expr* OsParseSubExpr(char* s, int len)
    {
        Tok* savedToks = _osToks;
        int savedCount = _osTokCount, savedPos = _osPos;
        Tok* subToks = (Tok*)OsAlloc(256 * sizeof(Tok));
        int subCount = OsLex(s, len, subToks, 256);
        if (_osErr) { _osToks = savedToks; _osTokCount = savedCount; _osPos = savedPos; return null; }
        _osToks = subToks; _osTokCount = subCount; _osPos = 0;
        Expr* e = OsParseExpr();
        _osToks = savedToks; _osTokCount = savedCount; _osPos = savedPos;
        return e;
    }

    // ==================================================================
    //  SCOPES / VARIABLES
    // ==================================================================
    private static void OsPushScope()
    {
        Scope* sc = (Scope*)OsAlloc(sizeof(Scope));
        sc->E = (VarEntry*)OsAlloc(8 * sizeof(VarEntry));
        sc->C = 0; sc->Cap = 8;
        sc->Parent = _osScope;
        _osScope = (ulong)sc;
    }

    private static void OsPopScope()
    {
        Scope* sc = (Scope*)_osScope;
        if (sc != null) _osScope = sc->Parent;
    }

    private static void OsScopeAdd(Scope* sc, char* name, int len, OslVal v)
    {
        if (sc->C >= sc->Cap)
        {
            int nc = sc->Cap * 2;
            VarEntry* ne = (VarEntry*)OsAlloc(nc * sizeof(VarEntry));
            for (int i = 0; i < sc->C; i++) ne[i] = sc->E[i];
            sc->E = ne; sc->Cap = nc;
        }
        sc->E[sc->C].N = name;
        sc->E[sc->C].NL = len;
        sc->E[sc->C].V = v;
        sc->C++;
    }

    // Whole scope chain including the globals; used for the ME/THIS lookup, which
    // must find the innermost binding of the current object.
    private static OslVal* OsFindVar(char* name, int len)
    {
        ulong sp = _osScope;
        while (sp != 0)
        {
            Scope* sc = (Scope*)sp;
            for (int i = 0; i < sc->C; i++)
                if (sc->E[i].NL == len && OsNameCI(sc->E[i].N, sc->E[i].NL, name, len))
                    return &sc->E[i].V;
            sp = sc->Parent;
        }
        return null;
    }

    // Locals and parameters, walking the chain of enclosing calls but stopping
    // before the global scope: the global root is a separate resolution step
    // (it loses against the current class, which OsFindVar could not express).
    // The walk matters because a call evaluates its arguments in the caller's
    // scope, and a recursive call therefore reads the enclosing frame's
    // parameters - `F(n + 1)` inside F must see `n`.
    private static OslVal* OsFindLocal(char* name, int len)
    {
        ulong sp = _osScope;
        while (sp != 0 && sp != _osGlobalScope)
        {
            Scope* sc = (Scope*)sp;
            for (int i = 0; i < sc->C; i++)
                if (sc->E[i].NL == len && OsNameCI(sc->E[i].N, sc->E[i].NL, name, len))
                    return &sc->E[i].V;
            sp = sc->Parent;
        }
        return null;
    }

    // Search only the root scope (the globals), which must lose against the
    // current class in the OSLANG 0.73 resolution order.
    private static OslVal* OsFindGlobal(char* name, int len)
    {
        Scope* gs = (Scope*)_osGlobalScope;
        if (gs == null) return null;
        for (int i = 0; i < gs->C; i++)
            if (gs->E[i].NL == len && OsNameCI(gs->E[i].N, gs->E[i].NL, name, len))
                return &gs->E[i].V;
        return null;
    }

    // The object the current method runs on, or NULL outside a method.
    private static OslVal OsCurrentObject()
    {
        fixed (char* meName = "ME")
        {
            OslVal* p = OsFindVar(meName, 2);
            if (p != null) return *p;
        }
        fixed (char* thisName = "THIS")
        {
            OslVal* p = OsFindVar(thisName, 4);
            if (p != null) return *p;
        }
        return OsNull();
    }

    // Index of a field in an object's layout (own fields first, then the ones
    // inherited from each parent), or -1 when the class has no such field.
    private static int OsFieldIndex(int classId, char* name, int len)
    {
        if (classId < 0 || classId >= _osClassCount) return -1;
        ClassDef* cls = &_osClasses[classId];
        int idx = 0;
        while (cls != null)
        {
            for (int i = 0; i < cls->FieldCount; i++, idx++)
                if (cls->FieldLens[i] == len && OsNameCI(cls->Fields[i], cls->FieldLens[i], name, len))
                    return idx;
            cls = cls->ParentClassId >= 0 ? &_osClasses[cls->ParentClassId] : null;
        }
        return -1;
    }

    // True when the current class (or a parent) declares `name` as a field.
    private static bool OsHasCurrentField(char* name, int len)
    {
        return _osCurrentClassId >= 0 && OsFieldIndex(_osCurrentClassId, name, len) >= 0;
    }

    // Assignment to a bare identifier, following the OSLANG 0.73 order:
    //   1. current local scope (locals and parameters)
    //   2. current class properties (implicit ME)
    //   3. globals
    // Enum types, functions and classes are not assignment targets, and a name
    // that matches nothing becomes a new local in the current scope. Reads use
    // the same order (OsResolveIdentifier), so a read and a write of the same
    // bare name can never disagree about which one they mean (B2).
    private static void OsAssign(char* name, int len, OslVal v)
    {
        OslVal* p = OsFindLocal(name, len);
        if (p != null) { *p = v; return; }

        OslVal me = OsCurrentObject();
        if (me.K == VKObj)
        {
            byte* mem = (byte*)me.P;
            int idx = OsFieldIndex(*(int*)mem, name, len);
            int total = *(int*)(mem + 4);
            if (idx >= 0 && idx < total)
            {
                ((OslVal*)(mem + 8))[idx] = v;
                return;
            }
        }

        p = OsFindGlobal(name, len);
        if (p != null) { *p = v; return; }

        Scope* sc = (Scope*)_osScope;
        if (sc != null) OsScopeAdd(sc, name, len, v);
    }

    private static void OsDeclareGlobal(char* name, int len, OslVal v)
    {
        Scope* gs = (Scope*)_osGlobalScope;
        // update existing global or add
        for (int i = 0; gs != null && i < gs->C; i++)
            if (gs->E[i].NL == len && OsNameCI(gs->E[i].N, gs->E[i].NL, name, len)) { gs->E[i].V = v; return; }
        if (gs != null) OsScopeAdd(gs, name, len, v);
    }

    // ==================================================================
    //  FUNCTION TABLE / CALLS
    // ==================================================================
    private static FuncDef* OsFindFunc(char* name, int len)
    {
        for (int i = 0; i < _osFuncCount; i++)
            if (_osFuncs[i].NL == len && OsNameCI(_osFuncs[i].N, _osFuncs[i].NL, name, len))
                return &_osFuncs[i];
        return null;
    }

    private static OslVal OsCallFunction(FuncDef* f, Expr** args, int argc)
    {
        // Recursion guard (B7). The error is raised through OsFail so the caller's
        // CATCH can handle it, and every early return below unwinds the counter.
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;

        OsPushScope();
        Scope* sc = (Scope*)_osScope;
        for (int i = 0; i < f->PC; i++)
        {
            OslVal v = (i < argc) ? OsEval(args[i]) : OsNull();
            if (_osErr) { OsPopScope(); _osCallDepth--; return OsNull(); }
            OsScopeAdd(sc, f->P[i], f->PL[i], v);
        }
        OsExecBlock(f->Body, f->BL);
        OslVal ret = (_osCtl == CTL_RETURN) ? _osRet : OsNull();
        // A pending exception belongs to the caller's TRY: only the control
        // signals (return/break/continue) are absorbed by the call itself.
        if (_osCtl != CTL_EXCEPTION) _osCtl = CTL_NONE;
        OsPopScope();
        _osCallDepth--;
        return ret;
    }

    // ==================================================================
    //  STATEMENT EXECUTION
    // ==================================================================
    private static void OsExecBlock(Stmt* body, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_osErr || _osExit) return;
            if (_osCtl != CTL_NONE) return; // break/continue/return propagates
            OsExec(&body[i]);
        }
    }

    private static void OsExec(Stmt* s)
    {
        if (_osErr || _osExit) return;
        switch (s->K)
        {
            case Sk.Print:
            case Sk.Show:
            {
                for (int i = 0; i < s->AC; i++)
                {
                    OslVal v = OsEval(s->Args[i]);
                    if (_osErr) return;
                    OsPrintVal(v);
                }
                if (s->K == Sk.Print) Vga.PutChar('\n');
                return;
            }
            case Sk.Input:
            {
                char* buf = stackalloc char[256];
                int l = OsReadLine(buf, 256);
                OslVal v = OsStr(buf, l);
                OsAssign(s->N, s->NL, v);
                return;
            }
            case Sk.Clear:
                Vga.Clear();
                return;
            case Sk.Assign:
            {
                OslVal v = OsEval(s->E2);
                if (_osErr) return;
                OsAssignTarget(s->E1, v);
                return;
            }
            case Sk.Expr:
                OsEval(s->E1);
                return;
            case Sk.If:
            {
                OslVal cond = OsEval(s->E1);
                if (_osErr) return;
                if (OsTruthy(cond))
                {
                    OsExecBlock(s->Body, s->BL);
                }
                else if (s->ElseBody != null)
                {
                    OsExecBlock(s->ElseBody, s->EL);
                }
                return;
            }
            case Sk.While:
            {
                while (true)
                {
                    if (_osErr || _osExit) return;
                    OslVal cond = OsEval(s->E1);
                    if (_osErr) return;
                    if (!OsTruthy(cond)) break;
                    OsExecBlock(s->Body, s->BL);
                    if (_osCtl == CTL_BREAK) { _osCtl = CTL_NONE; break; }
                    if (_osCtl == CTL_RETURN) return;
                    _osCtl = CTL_NONE; // clear continue / normal
                }
                return;
            }
            case Sk.DoWhile:
            {
                OslVal cond = default;
                do
                {
                    if (_osErr || _osExit) return;
                    OsExecBlock(s->Body, s->BL);
                    if (_osCtl == CTL_BREAK) { _osCtl = CTL_NONE; break; }
                    if (_osCtl == CTL_RETURN) return;
                    _osCtl = CTL_NONE; // clear continue / normal
                    if (_osErr || _osExit) return;
                    cond = OsEval(s->E1);
                    if (_osErr) return;
                } while (OsTruthy(cond));
                return;
            }
            case Sk.TryCatch:
            {
                // Execute TRY block
                OsExecBlock(s->Body, s->BL);
                if (_osCtl == CTL_EXCEPTION)
                {
                    // Exception caught - store error message in variable and execute CATCH block
                    OslVal errStr = OsStr(_osErrMsgPtr, _osErrMsgLen);
                    OsAssign(s->N, s->NL, errStr);
                    _osCtl = CTL_NONE;
                    _osErr = false; // Clear error so CATCH block can run
                    _osErrMsgLen = 0;
                    OsExecBlock(s->ElseBody, s->EL);
                    if (_osCtl == CTL_BREAK || _osCtl == CTL_RETURN) return;
                    _osCtl = CTL_NONE;
                }
                else if (_osCtl == CTL_BREAK || _osCtl == CTL_RETURN)
                {
                    // Control flow propagates out
                    return;
                }
                return;
            }
            case Sk.For:
            {
                OslVal fromV = OsEval(s->E1); if (_osErr) return;
                OslVal toV = OsEval(s->E2); if (_osErr) return;
                double start = OsToNum(fromV, "FOR start");
                double end = OsToNum(toV, "FOR end");
                double step = 1;
                if (s->E3 != null)
                {
                    OslVal stV = OsEval(s->E3); if (_osErr) return;
                    step = OsToNum(stV, "STEP");
                }
                if (step == 0) { OsFail("STEP cannot be zero."); return; }
                double cur = start;
                while (step > 0 ? cur <= end : cur >= end)
                {
                    if (_osErr || _osExit) return;
                    OsAssign(s->N, s->NL, OsNum(cur));
                    OsExecBlock(s->Body, s->BL);
                    if (_osCtl == CTL_BREAK) { _osCtl = CTL_NONE; break; }
                    if (_osCtl == CTL_RETURN) return;
                    _osCtl = CTL_NONE; // clear continue / normal
                    cur += step;
                }
                return;
            }
            case Sk.Return:
            {
                _osRet = (s->E1 != null) ? OsEval(s->E1) : OsNull();
                if (_osErr) return;
                _osCtl = CTL_RETURN;
                return;
            }
            case Sk.Var:
            {
                OslVal v = (s->E1 != null) ? OsEval(s->E1) : OsNull();
                if (_osErr) return;
                OsAssign(s->N, s->NL, v);
                return;
            }
            case Sk.Global:
            {
                OslVal v = (s->E1 != null) ? OsEval(s->E1) : OsNull();
                if (_osErr) return;
                OsDeclareGlobal(s->N, s->NL, v);
                return;
            }
            case Sk.Break:
                _osCtl = CTL_BREAK;
                return;
            case Sk.Continue:
                _osCtl = CTL_CONTINUE;
                return;
            default:
                return;
        }
    }

    private static double OsToNum(OslVal v, string what)
    {
        switch (v.K)
        {
            case VKNum: return v.D;
            case VKBool: return v.B ? 1 : 0;
            case VKNull: return 0;
            case VKStr:
            {
                char* buf = stackalloc char[64];
                int n = v.L < 64 ? v.L : 64;
                char* s = (char*)v.P;
                for (int i = 0; i < n; i++) buf[i] = s[i];
                return OsParseNumber(buf, n);
            }
            default:
                OsFail2(what, " must be a number.");
                return 0;
        }
    }

    // Assignment target: Id (variable), Index (array element), or Member (object field).
    private static void OsAssignTarget(Expr* target, OslVal v)
    {
        if (target->K == Ek.Id)
        {
            OsAssign(target->N, target->NL, v);
            return;
        }
        if (target->K == Ek.Index)
        {
            OslVal arr = OsEval(target->A);
            if (_osErr) return;
            OslVal idx = OsEval(target->B);
            if (_osErr) return;
            if (arr.K == VKStr) { OsFail("Cannot assign to a string index."); return; }
            if (arr.K == VKArr)
            {
                if (idx.K != VKNum) { OsFail("Array index must be a number."); return; }
                int i = (int)idx.D;
                if (i < 0 || i >= OsArrCount(arr)) { OsFail("Array index out of range."); return; }
                OsArrElem(arr)[i] = v;
                return;
            }
            OsFail("Cannot index-assign this value.");
            return;
        }
        if (target->K == Ek.Member)
        {
            // Object field assignment: obj.field = value
            OslVal obj = OsEval(target->A);
            if (_osErr) return;
            if (obj.K == VKObj && obj.L != -1)
            {
                OsSetObjectField(obj, target->N, target->NL, v);
                return;
            }
            OsFail("Cannot assign to member of this value.");
            return;
        }
        OsFail("Invalid assignment target.");
    }

    // ==================================================================
    //  EXPRESSION EVALUATION
    // ==================================================================
    private static OslVal OsEval(Expr* e)
    {
        if (_osErr) return OsNull();
        switch (e->K)
        {
            case Ek.Num: return e->V;
            case Ek.Str: return e->V;
            case Ek.Bool: return e->V;
            case Ek.Null: return OsNull();
            case Ek.Id: return OsResolveIdentifier(e->N, e->NL);
            case Ek.Bin: return OsEvalBin(e);
            case Ek.Un: return OsEvalUn(e);
            case Ek.Call: return OsEvalCall(e);
            case Ek.Member: return OsEvalMember(e);
            case Ek.Index: return OsEvalIndex(e);
            case Ek.Arr: return OsEvalArr(e);
            case Ek.Interp: return OsEvalInterp(e);
            case Ek.NewObj: return OsEvalNewObj(e);
            case Ek.This: return OsEvalThis();
            case Ek.EnumVal: return OsEvalEnumVal(e);
            case Ek.ClassRef: return OsEvalClassRef(e);
        }
        return OsNull();
    }

    // Identifier resolution per OSLANG 0.73 §11-18:
    //   1. current local scope (locals and parameters)
    //   2. current class properties, then the parent chain (implicit ME)
    //   3. globals
    //   4. enum types
    // Explicit ME.X bypasses this search (see OsEvalMember). Steps 5 (functions)
    // and 6 (classes/interfaces) are not reachable as bare identifiers yet.
    private static OslVal OsResolveIdentifier(char* name, int len)
    {
        // 1. Current scope (local variables and parameters)
        OslVal* p = OsFindLocal(name, len);
        if (p != null) return *p;

        // 2./3. Current class (if we're in a method), then its parents
        if (_osCurrentClassId >= 0 && OsFieldIndex(_osCurrentClassId, name, len) >= 0)
        {
            OslVal me = OsEvalThis();
            if (me.K == VKObj) return OsGetObjectField(me, name, len);
            return OsNull();
        }

        // 4. Globals
        p = OsFindGlobal(name, len);
        if (p != null) return *p;

        // 5. Enum types (KEY.ENTER style access)
        for (int i = 0; i < _osEnumCount; i++)
        {
            EnumDef* en = &_osEnums[i];
            if (en->NL == len && OsNameCI(en->N, en->NL, name, len))
            {
                // Return the enum type itself (for KEY.ENTER style access)
                OslVal v = default;
                v.K = VKEnum;
                v.P = 0; // not a value, but a type reference
                v.L = i; // enum type id
                return v;
            }
        }

        OsFailName(name, len, "Undefined identifier '");
        return OsNull();
    }

    private static OslVal OsEvalThis()
    {
        // ME/THIS is a special variable in the current scope
        OslVal me = OsCurrentObject();
        if (me.K == VKObj) return me;
        OsFail("ME/THIS used outside object context.");
        return OsNull();
    }

    private static OslVal OsEvalNewObj(Expr* e)
    {
        // Find the class
        int classId = -1;
        for (int i = 0; i < _osClassCount; i++)
        {
            if (_osClasses[i].NL == e->NL && OsNameCI(_osClasses[i].N, _osClasses[i].NL, e->N, e->NL))
            {
                classId = i;
                break;
            }
        }
        if (classId == -1) { OsFailName(e->N, e->NL, "Class not found: '"); return OsNull(); }

        ClassDef* cls = &_osClasses[classId];

        // Create object instance
        // Object layout: [int classId][int fieldCount][OslVal fields...]
        int totalFields = cls->FieldCount;
        if (cls->ParentClassId >= 0)
        {
            totalFields += _osClasses[cls->ParentClassId].FieldCount;
        }

        int objSize = 8 + totalFields * sizeof(OslVal); // classId + fieldCount + fields
        byte* mem = (byte*)OsAlloc(objSize);
        *(int*)mem = classId;
        *(int*)(mem + 4) = totalFields;
        OslVal* fields = (OslVal*)(mem + 8);

        // Initialize fields to NULL
        for (int i = 0; i < totalFields; i++)
        {
            fields[i] = OsNull();
        }

        // Call constructor if exists (method named CONSTRUCTOR)
        // Check current class and parent classes
        ClassDef* currentCls = cls;
        while (currentCls != null)
        {
            for (int i = 0; i < currentCls->MethodCount; i++)
            {
                if (currentCls->Methods[i] != null && currentCls->Methods[i][0] != '\0')
                {
                    // Check if method name is "CONSTRUCTOR"
                    bool isConstructor = true;
                    fixed (char* ctorName = "CONSTRUCTOR")
                    {
                        for (int j = 0; j < 11; j++)
                        {
                            if (currentCls->Methods[i][j] != ctorName[j]) { isConstructor = false; break; }
                        }
                        if (isConstructor && currentCls->Methods[i][11] == '\0')
                        {
                            // Found constructor - call it with the provided arguments
                            OslVal thisObj = default;
                            thisObj.K = VKObj;
                            thisObj.P = (ulong)mem;
                            thisObj.L = totalFields;
                            OsCallMethod(currentCls, i, thisObj, e->Args, e->AC);
                            return thisObj;
                        }
                    }
                }
            }
            // Move to parent class
            if (currentCls->ParentClassId >= 0)
            {
                currentCls = &_osClasses[currentCls->ParentClassId];
            }
            else
            {
                currentCls = null;
            }
        }

        OslVal obj = default;
        obj.K = VKObj;
        obj.P = (ulong)mem;
        obj.L = totalFields;
        return obj;
    }

    private static OslVal OsGetObjectField(OslVal obj, char* name, int len)
    {
        if (obj.K != VKObj) return OsNull();
        byte* mem = (byte*)obj.P;
        int classId = *(int*)mem;
        int fieldCount = *(int*)(mem + 4);
        OslVal* fields = (OslVal*)(mem + 8);

        // Search in class and parent
        ClassDef* cls = &_osClasses[classId];
        int fieldIndex = 0;

        // Check current class fields
        for (int i = 0; i < cls->FieldCount; i++)
        {
            if (cls->FieldLens[i] == len && OsNameCI(cls->Fields[i], cls->FieldLens[i], name, len))
            {
                return fields[fieldIndex];
            }
            fieldIndex++;
        }

        // Check parent class fields
        if (cls->ParentClassId >= 0)
        {
            ClassDef* parent = &_osClasses[cls->ParentClassId];
            for (int i = 0; i < parent->FieldCount; i++)
            {
                if (parent->FieldLens[i] == len && OsNameCI(parent->Fields[i], parent->FieldLens[i], name, len))
                {
                    return fields[fieldIndex];
                }
                fieldIndex++;
            }
        }

        return OsNull();
    }

    private static void OsSetObjectField(OslVal obj, char* name, int len, OslVal value)
    {
        if (obj.K != VKObj) return;
        byte* mem = (byte*)obj.P;
        int classId = *(int*)mem;
        int fieldCount = *(int*)(mem + 4);
        OslVal* fields = (OslVal*)(mem + 8);

        ClassDef* cls = &_osClasses[classId];
        int fieldIndex = 0;

        for (int i = 0; i < cls->FieldCount; i++)
        {
            if (cls->FieldLens[i] == len && OsNameCI(cls->Fields[i], cls->FieldLens[i], name, len))
            {
                fields[fieldIndex] = value;
                return;
            }
            fieldIndex++;
        }

        if (cls->ParentClassId >= 0)
        {
            ClassDef* parent = &_osClasses[cls->ParentClassId];
            for (int i = 0; i < parent->FieldCount; i++)
            {
                if (parent->FieldLens[i] == len && OsNameCI(parent->Fields[i], parent->FieldLens[i], name, len))
                {
                    fields[fieldIndex] = value;
                    return;
                }
                fieldIndex++;
            }
        }
    }

    private static OslVal OsEvalEnumVal(Expr* e)
    {
        // e->N is the enum type name, e->NL is its length
        // e->A should be the member access expression
        // Actually, enum member access is handled via Member expression (KEY.ENTER)
        // So this might not be needed directly
        return OsNull();
    }

    private static OslVal OsEvalClassRef(Expr* e)
    {
        // Return a reference to the class type
        for (int i = 0; i < _osClassCount; i++)
        {
            if (_osClasses[i].NL == e->NL && OsNameCI(_osClasses[i].N, _osClasses[i].NL, e->N, e->NL))
            {
                OslVal v = default;
                v.K = VKObj; // Using VKObj as class reference marker
                v.P = (ulong)i; // class ID
                v.L = -1; // marker for class reference
                return v;
            }
        }
        return OsNull();
    }

    private static OslVal OsEvalBin(Expr* e)
    {
        if (e->Op == OP_AND)
        {
            bool lt = OsTruthy(OsEval(e->A));
            if (_osErr) return OsNull();
            if (!lt) return OsBool(false);
            return OsBool(OsTruthy(OsEval(e->B)));
        }
        if (e->Op == OP_OR)
        {
            bool lt = OsTruthy(OsEval(e->A));
            if (_osErr) return OsNull();
            if (lt) return OsBool(true);
            return OsBool(OsTruthy(OsEval(e->B)));
        }
        if (e->Op == OP_BOR)
        {
            // Bitwise OR for enum flag combination
            OslVal lv = OsEval(e->A);
            if (_osErr) return OsNull();
            OslVal rv = OsEval(e->B);
            if (_osErr) return OsNull();
            
            // Both must be enum values or numbers
            if (lv.K == VKEnum && rv.K == VKEnum)
            {
                // Combine enum flags: return new enum value with combined raw value
                OslVal result = default;
                result.K = VKEnum;
                result.P = lv.P | rv.P; // raw value
                result.L = lv.L; // enum type id
                return result;
            }
            if (lv.K == VKNum && rv.K == VKNum)
            {
                return OsNum((double)((ulong)lv.D | (ulong)rv.D));
            }
            if (lv.K == VKEnum && rv.K == VKNum)
            {
                OslVal result = default;
                result.K = VKEnum;
                result.P = lv.P | (ulong)rv.D;
                result.L = lv.L;
                return result;
            }
            if (lv.K == VKNum && rv.K == VKEnum)
            {
                OslVal result = default;
                result.K = VKEnum;
                result.P = (ulong)lv.D | rv.P;
                result.L = rv.L;
                return result;
            }
            OsFail("Invalid operands for '|' (enum flag combination).");
            return OsNull();
        }
        OslVal l = OsEval(e->A);
        if (_osErr) return OsNull();
        OslVal r = OsEval(e->B);
        if (_osErr) return OsNull();
        switch (e->Op)
        {
            case OP_ADD:
                if (l.K == VKStr || r.K == VKStr) return OsConcat(l, r);
                if (l.K == VKNum && r.K == VKNum) return OsNum(l.D + r.D);
                OsFail("Invalid '+' operands."); return OsNull();
            case OP_SUB: if (!OsNums(l, r, "-")) return OsNull(); return OsNum(l.D - r.D);
            case OP_MUL: if (!OsNums(l, r, "*")) return OsNull(); return OsNum(l.D * r.D);
            case OP_DIV:
                if (!OsNums(l, r, "/")) return OsNull();
                if (r.D == 0) { OsFail("Division by zero."); return OsNull(); }
                return OsNum(l.D / r.D);
            case OP_MOD:
                if (!OsNums(l, r, "%")) return OsNull();
                if (r.D == 0) { OsFail("Division by zero."); return OsNull(); }
                return OsNum(l.D - OsFloor(l.D / r.D) * r.D);
            case OP_POW: if (!OsNums(l, r, "**")) return OsNull(); return OsNum(OsPow(l.D, r.D));
            case OP_EQ: return OsBool(OsValuesEqual(l, r));
            case OP_NE: return OsBool(!OsValuesEqual(l, r));
            case OP_LT: if (!OsNums(l, r, "<")) return OsNull(); return OsBool(l.D < r.D);
            case OP_GT: if (!OsNums(l, r, ">")) return OsNull(); return OsBool(l.D > r.D);
            case OP_LE: if (!OsNums(l, r, "<=")) return OsNull(); return OsBool(l.D <= r.D);
            case OP_GE: if (!OsNums(l, r, ">=")) return OsNull(); return OsBool(l.D >= r.D);
        }
        OsFail("Unknown operator.");
        return OsNull();
    }

    private static bool OsNums(OslVal l, OslVal r, string op)
    {
        if (l.K != VKNum || r.K != VKNum)
        {
            if (!_osErr)
            {
                OsRaise("");
                OsErrAppendStr("Operator '");
                OsErrAppendStr(op);
                OsErrAppendStr("' requires two numbers.");
                Vga.Write("OSLANG error: Operator '");
                Vga.Write(op);
                Vga.WriteLine("' requires two numbers.");
            }
            return false;
        }
        return true;
    }

    private static OslVal OsEvalUn(Expr* e)
    {
        if (e->Op == OP_NOT) return OsBool(!OsTruthy(OsEval(e->A)));
        if (e->Op == OP_NEG)
        {
            OslVal v = OsEval(e->A);
            if (_osErr) return OsNull();
            if (v.K != VKNum) { OsFail("Unary '-' requires a number."); return OsNull(); }
            return OsNum(-v.D);
        }
        return OsNull();
    }

    // Build a dotted name from an Id or Member chain (e.g. OSL.CONSOLE.PRINT).
    private static int OsBuildName(Expr* e, char* buf, int cap)
    {
        if (e->K == Ek.Id)
        {
            int n = 0;
            while (n < cap && n < e->NL) { buf[n] = e->N[n]; n++; }
            return n;
        }
        if (e->K == Ek.Member)
        {
            int n = OsBuildName(e->A, buf, cap);
            if (n < 0) return -1;
            if (n < cap) { buf[n] = '.'; n++; }
            int m = 0;
            while (n < cap && m < e->NL) { buf[n] = e->N[m]; n++; m++; }
            return n;
        }
        return -1;
    }

    private static OslVal OsEvalCall(Expr* e)
    {
        // Check if it's a namespace call (OSL.CONSOLE.GETKEY, OSL.FILE.READ, etc.)
        // Detect by checking if the callee is a Member chain starting with "OSL"
        if (e->A->K == Ek.Member)
        {
            Expr* baseExpr = e->A;
            // Traverse to the leftmost member
            while (baseExpr->A->K == Ek.Member)
            {
                baseExpr = baseExpr->A;
            }
            // Now baseExpr->A should be the root identifier
            if (baseExpr->A->K == Ek.Id && OsNameEq(baseExpr->A->N, baseExpr->A->NL, "MATH"))
            {
                char* buf2 = stackalloc char[128];
                int nlen2 = OsBuildName(e->A, buf2, 128);
                if (nlen2 < 0) { OsFail("Cannot call this expression."); return OsNull(); }
                return OsStdlib(buf2, nlen2, e->Args, e->AC);
            }
            if (baseExpr->A->K == Ek.Id && OsNameEq(baseExpr->A->N, baseExpr->A->NL, "OSL"))
            {
                // This is an OSL namespace call - build full name and call stdlib
                char* buf2 = stackalloc char[128];
                int nlen2 = OsBuildName(e->A, buf2, 128);
                if (nlen2 < 0) { OsFail("Cannot call this expression."); return OsNull(); }
                return OsStdlib(buf2, nlen2, e->Args, e->AC);
            }
        }

        // Check if it's a method call on an object (obj.method())
        if (e->A->K == Ek.Member)
        {
            OslVal obj = OsEval(e->A->A); // the object
            if (_osErr) return OsNull();

            // Get method name
            char* methodName = e->A->N;
            int methodNameLen = e->A->NL;

            OslVal _primResult;
            if (OsValueMethod(obj, methodName, methodNameLen, e->Args, e->AC, out _primResult))
            {
                return _primResult;
            }
            else
            {
                Vga.Write("PRIMMISS ");
                for (int _i=0; _i<methodNameLen && _i<32; _i++) Vga.PutChar(methodName[_i]);
                Vga.WriteLine("");
            }

            if (obj.K == VKObj && obj.L != -1)
            {
                // Instance method call - but not a namespace reference (namespace refs have P pointing to string)
                // Instance objects have P pointing to object memory, namespace refs have P pointing to name string
                // We can distinguish by checking if the object has a valid classId in its memory
                byte* mem = (byte*)obj.P;
                int classId = *(int*)mem;
                if (classId >= 0 && classId < _osClassCount)
                {
                    ClassDef* cls = &_osClasses[classId];

                    // Find method by name
                    for (int i = 0; i < cls->MethodCount; i++)
                    {
                        if (cls->MethodBodies[i] != null && cls->Methods[i] != null)
                        {
                            if (cls->Methods[i] != null && cls->Methods[i][0] != '\0')
                            {
                                // Compare method name
                                int j = 0;
                                bool match = true;
                                for (; j < methodNameLen; j++)
                                {
                                    if (cls->Methods[i][j] != methodName[j]) { match = false; break; }
                                }
                                if (match && cls->Methods[i][j] == '\0')
                                {
                                    return OsCallMethod(cls, i, obj, e->Args, e->AC);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Global function call
        char* buf = stackalloc char[128];
        int nlen = OsBuildName(e->A, buf, 128);
        if (nlen < 0) { OsFail("Cannot call this expression."); return OsNull(); }
        FuncDef* f = OsFindFunc(buf, nlen);
        if (f != null) return OsCallFunction(f, e->Args, e->AC);
        return OsStdlib(buf, nlen, e->Args, e->AC);
    }

    private static OslVal OsCallMethod(ClassDef* cls, int methodIndex, OslVal thisObj, Expr** args, int argc)
    {
        char* _prevMethod = _osCurrentMethodName;
        int _prevMethodLen = _osCurrentMethodNameLen;
        _osCurrentMethodName = cls->Methods[methodIndex];
        _osCurrentMethodNameLen = (cls->Methods[methodIndex] != null) ? (int)cls->MethodParamCounts[methodIndex]*0 + 0 : 0;
        // method name length not stored? count manually:
        int _mn = 0; while (_osCurrentMethodName != null && _osCurrentMethodName[_mn] != '\0') _mn++;
        _osCurrentMethodNameLen = _mn;
        // Shares the recursion counter with OsCallFunction (B7).
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;

        OsPushScope();
        Scope* sc = (Scope*)_osScope;

        // Bind ME/THIS to the current object
        fixed (char* meName = "ME")
        {
            OsScopeAdd(sc, meName, 2, thisObj);
        }
        fixed (char* thisName = "THIS")
        {
            OsScopeAdd(sc, thisName, 4, thisObj);
        }

        // Bind method parameters
        int paramCount = cls->MethodParamCounts[methodIndex];
        if (paramCount > 0 && cls->MethodParamNames[methodIndex] != null)
        {
            char** paramNames = cls->MethodParamNames[methodIndex];
            int* paramLens = cls->MethodParamLens[methodIndex];
            for (int i = 0; i < paramCount; i++)
            {
                OslVal v = (i < argc) ? OsEval(args[i]) : OsNull();
                if (_osErr) { OsPopScope(); _osCallDepth--; return OsNull(); }
                OsScopeAdd(sc, paramNames[i], paramLens[i], v);
            }
        }

        // Set current class for ME resolution
        int prevClassId = _osCurrentClassId;
        // Find class index
        int classIndex = -1;
        for (int i = 0; i < _osClassCount; i++)
        {
            if (&_osClasses[i] == cls) { classIndex = i; break; }
        }
        _osCurrentClassId = classIndex;

        OsExecBlock(cls->MethodBodies[methodIndex], cls->MethodBodyLens[methodIndex]);

        OslVal ret = (_osCtl == CTL_RETURN) ? _osRet : OsNull();
        if (_osCtl != CTL_EXCEPTION) _osCtl = CTL_NONE; // see OsCallFunction

        _osCurrentClassId = prevClassId;
        OsPopScope();
        _osCallDepth--;
        return ret;
    }

    private static OslVal OsEvalMember(Expr* e)
    {
        // Handle known namespace prefixes (OSL.CONSOLE, OSL.FILE, OSL.APP)
        // Check if the base is a simple identifier that's a known namespace
        if (e->A->K == Ek.Id)
        {
            // Check for OSL.CONSOLE, OSL.FILE, OSL.APP
            if (OsNameEq(e->A->N, e->A->NL, "OSL"))
            {
                // Build the full namespace name: OSL.CONSOLE, OSL.FILE, etc.
                char* buf = stackalloc char[64];
                int n = 0;
                // Copy "OSL"
                fixed (char* osl = "OSL") { for (int i = 0; i < 3; i++) buf[n++] = osl[i]; }
                buf[n++] = '.';
                // Copy member name (CONSOLE, FILE, APP)
                for (int i = 0; i < e->NL && n < 63; i++) buf[n++] = e->N[i];
                
                // Return a special namespace reference object
                OslVal nsRef = default;
                nsRef.K = VKObj;
                nsRef.P = 0; // namespace marker
                nsRef.L = n; // length of namespace name
                // Store namespace name in arena for later lookup
                char* nsName = (char*)OsAlloc(n + 1);
                for (int i = 0; i < n; i++) nsName[i] = buf[i];
                nsName[n] = '\0';
                nsRef.P = (ulong)nsName;
                return nsRef;
            }
        }

        OslVal obj = OsEval(e->A);
        if (_osErr) return OsNull();

        // Handle keyboard event object (classId = -1, 5 fields: KEY, CHAR, CTRL, ALT, SHIFT)
        if (obj.K == VKObj)
        {
            byte* mem = (byte*)obj.P;
            int classId = *(int*)mem;
            int fieldCount = *(int*)(mem + 4);
            if (classId == -2 && fieldCount == 2)
            {
                OslVal* fields = (OslVal*)(mem + 8);
                // Support field-name access even though special object lacks class metadata.
                if (OsNameEq(e->N, e->NL, "WIDTH")) return fields[0];
                if (OsNameEq(e->N, e->NL, "HEIGHT")) return fields[1];
            }
            if (classId == -1 && fieldCount == 5)
            {
                // Keyboard event object - map field names to indices
                OslVal* fields = (OslVal*)(mem + 8);
                if (OsNameEq(e->N, e->NL, "KEY")) return fields[0];
                if (OsNameEq(e->N, e->NL, "CHAR")) return fields[1];
                if (OsNameEq(e->N, e->NL, "CTRL")) return fields[2];
                if (OsNameEq(e->N, e->NL, "ALT")) return fields[3];
                if (OsNameEq(e->N, e->NL, "SHIFT")) return fields[4];
            }
        }

        // String/Array members
        if (OsNameEq(e->N, e->NL, "LENGTH") || OsNameEq(e->N, e->NL, "COUNT"))
        {
            if (obj.K == VKStr) return OsNum(obj.L);
            if (obj.K == VKArr) return OsNum(OsArrCount(obj));
        }

        // Object field/method access
        if (obj.K == VKObj)
        {
            // Check if it's a class reference (L == -1)
            if (obj.L == -1)
            {
                // Static member access on class: Class.Field or Class.Method
                int classId = (int)obj.P;
                ClassDef* cls = &_osClasses[classId];

                // Check fields
                for (int i = 0; i < cls->FieldCount; i++)
                {
                    if (cls->FieldLens[i] == e->NL && OsNameCI(cls->Fields[i], cls->FieldLens[i], e->N, e->NL))
                    {
                        // Static field - for now return NULL (not implemented)
                        return OsNull();
                    }
                }

                // Check methods
                for (int i = 0; i < cls->MethodCount; i++)
                {
                    if (cls->MethodBodies[i] != null)
                    {
                        // Method reference - handled in OsEvalCall
                    }
                }
            }
            else
            {
                // Instance field access
                OslVal field = OsGetObjectField(obj, e->N, e->NL);
                if (field.K != VKNull || _osErr) return field;

                // Check for method
                byte* mem = (byte*)obj.P;
                int classId = *(int*)mem;
                ClassDef* cls = &_osClasses[classId];
                for (int i = 0; i < cls->MethodCount; i++)
                {
                    if (cls->MethodBodies[i] != null)
                    {
                        // Method call - will be handled in OsEvalCall
                        // Return a callable reference
                        OslVal methodRef = default;
                        methodRef.K = VKObj; // reuse for method reference
                        methodRef.P = (ulong)classId;
                        methodRef.L = i; // method index
                        return methodRef;
                    }
                }
            }
        }

        // Enum member access (KEY.ENTER)
        if (obj.K == VKEnum && obj.P == 0)
        {
            // This is an enum type reference, look up the member
            int enumId = obj.L;
            if (enumId >= 0 && enumId < _osEnumCount)
            {
                EnumDef* en = &_osEnums[enumId];
                for (int i = 0; i < en->MemberCount; i++)
                {
                    if (en->MemberLens[i] == e->NL && OsNameCI(en->Members[i], en->MemberLens[i], e->N, e->NL))
                    {
                        OslVal enumVal = default;
                        enumVal.K = VKEnum;
                        enumVal.P = en->Values[i];
                        enumVal.L = enumId;
                        return enumVal;
                    }
                }
            }
        }

        OsFailName(e->N, e->NL, "Unknown member '");
        return OsNull();
    }

    private static OslVal OsEvalIndex(Expr* e)
    {
        OslVal arr = OsEval(e->A);
        if (_osErr) return OsNull();
        OslVal idx = OsEval(e->B);
        if (_osErr) return OsNull();
        if (arr.K == VKStr)
        {
            if (idx.K != VKNum) { OsFail("String index must be a number."); return OsNull(); }
            int i = (int)idx.D;
            if (i < 0 || i >= arr.L) { OsFail("String index out of range."); return OsNull(); }
            char* s = (char*)arr.P;
            return OsStr(s + i, 1);
        }
        if (arr.K == VKArr)
        {
            if (idx.K != VKNum) { OsFail("Array index must be a number."); return OsNull(); }
            int i = (int)idx.D;
            if (i < 0 || i >= OsArrCount(arr)) { OsFail("Array index out of range."); return OsNull(); }
            return OsArrElem(arr)[i];
        }
        OsFail("Cannot index this value.");
        return OsNull();
    }

    private static OslVal OsEvalArr(Expr* e)
    {
        OslVal arr = OsArrMake(e->AC);
        if (arr.K != VKArr) return arr;   // arena exhausted; OsFail already raised
        for (int i = 0; i < e->AC; i++)
        {
            OslVal v = OsEval(e->Args[i]);
            if (_osErr) return OsNull();
            OsArrElem(arr)[i] = v;
        }
        // Publish the element count only once every element is in place, so a
        // half-built array is never visible (B3).
        OsArrSetCount(arr, e->AC);
        return arr;
    }

    private static OslVal OsEvalInterp(Expr* e)
    {
        OslVal result = OsStr(null, 0); // empty string
        for (int i = 0; i < e->AC; i++)
        {
            Expr* part = e->Args[i];
            OslVal piece;
            if (part->K == Ek.Str)
            {
                piece = part->V;
            }
            else
            {
                piece = OsEval(part);
                if (_osErr) return OsNull();
                if (piece.K != VKStr) piece = OsToDisplayStr(piece);
            }
            result = OsConcat(result, piece);
            if (_osErr) return OsNull();
        }
        return result;
    }

    // ==================================================================
    //  STANDARD LIBRARY
    // ==================================================================
    private static OslVal OsArg(Expr** args, int argc, int i)
    {
        if (i < argc) return OsEval(args[i]);
        return OsNull();
    }

    private static void OsPrintArgs(Expr** args, int argc, bool newline)
    {
        for (int i = 0; i < argc; i++)
        {
            OslVal v = OsEval(args[i]);
            if (_osErr) return;
            OsPrintVal(v);
        }
        if (newline) Vga.PutChar('\n');
    }

    private static int OsValToBuf(OslVal v, char* buf, int max)
    {
        if (v.K != VKStr) return 0;
        char* s = (char*)v.P;
        int n = v.L < max ? v.L : max;
        for (int i = 0; i < n; i++) buf[i] = s[i];
        return n;
    }

    private static OslVal OsStdlib(char* name, int len, Expr** args, int argc)
    {
        OslVal _helper;
        if (OsConsoleTryStdlib(name, len, args, argc, out _helper)) return _helper;
        if (OsFileTryStdlib(name, len, args, argc, out _helper)) return _helper;
        if (OsAppTryStdlib(name, len, args, argc, out _helper)) return _helper;
        if (OsMathTryStdlib(name, len, args, argc, out _helper)) return _helper;

        // ---- OSL.CONSOLE ----
        if (OsNameEq(name, len, "OSL.CONSOLE.PRINT")) { OsPrintArgs(args, argc, true); return OsNull(); }
        if (OsNameEq(name, len, "OSL.CONSOLE.SHOW")) { OsPrintArgs(args, argc, false); return OsNull(); }
        if (OsNameEq(name, len, "OSL.CONSOLE.CLEAR")) { Vga.Clear(); return OsNull(); }
        if (OsNameEq(name, len, "OSL.CONSOLE.INPUT") || OsNameEq(name, len, "OSL.CONSOLE.READ"))
        {
            char* buf = stackalloc char[256];
            int l = OsReadLine(buf, 256);
            return OsStr(buf, l);
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.WRITE"))
        {
            OslVal v = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            OsPrintVal(v);
            return OsNull();
        }
        if (OsNameEq(name, len, "OSL.CONSOLE.GETKEY"))
        {
            // Returns a keyboard event object with KEY, CHAR, CTRL, ALT, SHIFT fields
            // Per OSLANG 0.72 spec: KEY is enum KEY, CHAR is printable char or NULL
            int ch = Serial.Pop();
            int sc = -1;
            bool shift = false, ctrl = false, alt = false;
            if (ch == 27) sc = 0x01;   // treat serial ESC as KEYCODE.ESC
            if (ch < 0)
            {
                sc = Keyboard.Pop();
                if (sc >= 0)
                {
                    // Check for modifier keys
                    if (sc == 0x2A || sc == 0x36) { shift = true; sc = Keyboard.Pop(); }
                    if (sc == 0x1D) { ctrl = true; sc = Keyboard.Pop(); }
                    if (sc == 0x38) { alt = true; sc = Keyboard.Pop(); }
                    
                    if (sc >= 0 && (sc & 0x80) == 0)
                    {
                        char c = Keyboard.MapScancode((byte)sc, shift);
                        if (c != 0) ch = c;
                    }
                }
            }

            // Create keyboard event object
            // Object with fields: KEY (enum), CHAR (string), CTRL (bool), ALT (bool), SHIFT (bool)
            int fieldCount = 5;
            int objSize = 8 + fieldCount * sizeof(OslVal);
            byte* mem = (byte*)OsAlloc(objSize);
            *(int*)mem = -1; // special classId for keyboard event
            *(int*)(mem + 4) = fieldCount;
            OslVal* fields = (OslVal*)(mem + 8);

            // Find KEY enum
            int keyEnumId = -1;
            for (int i = 0; i < _osEnumCount; i++)
            {
                if (_osEnums[i].NL == 3)
                {
                    fixed (char* keyName = "KEY")
                    {
                        if (OsNameCI(_osEnums[i].N, _osEnums[i].NL, keyName, 3))
                        {
                            keyEnumId = i;
                            break;
                        }
                    }
                }
            }

            // KEY field
            OslVal keyVal = default;
            keyVal.K = VKEnum;
            if (sc >= 0 && keyEnumId >= 0)
            {
                // Map scancode to KEY enum value
                ulong keyValue = (ulong)sc;
                keyVal.P = keyValue;
                keyVal.L = keyEnumId;
            }
            else
            {
                keyVal.P = 0; // KEY.UNKNOWN
                keyVal.L = keyEnumId;
            }
            fields[0] = keyVal;

            // CHAR field
            OslVal charVal = default;
            if (ch >= 0 && ch != 0x1C && ch != 0x0E && ch != 0x01 && ch != 0x0F && ch != 0x1C && ch != 0x1D && ch != 0x2A && ch != 0x36 && ch != 0x38 && ch != 0x39)
            {
                char c = (char)ch;
                charVal = OsStr(&c, 1);
            }
            else
            {
                charVal = OsNull();
            }
            fields[1] = charVal;

            // CTRL field
            fields[2] = OsBool(ctrl);

            // ALT field
            fields[3] = OsBool(alt);

            // SHIFT field
            fields[4] = OsBool(shift);

            OslVal eventObj = default;
            eventObj.K = VKObj;
            eventObj.P = (ulong)mem;
            eventObj.L = fieldCount;
            return eventObj;
        }

        // ---- OSL.FILE (read side) ----
        if (OsNameEq(name, len, "OSL.FILE.READ"))
        {
            OslVal pv = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            return OsFileRead(pv);
        }
        if (OsNameEq(name, len, "OSL.FILE.EXISTS"))
        {
            OslVal pv = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            return OsBool(OsFileExists(pv));
        }
        if (OsNameEq(name, len, "OSL.FILE.SIZE"))
        {
            OslVal pv = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            return OsNum(OsFileSize(pv));
        }
        if (OsNameEq(name, len, "OSL.FILE.WRITE") || OsNameEq(name, len, "OSL.FILE.APPEND") ||
            OsNameEq(name, len, "OSL.FILE.DELETE"))
        {
            OsFail("OSL.FILE write operations are not supported yet.");
            return OsNull();
        }

        // ---- OSL.APP ----
        if (OsNameEq(name, len, "OSL.APP.ARGS"))
        {
            return OsStr(_osArgs, _osArgsLen);
        }
        if (OsNameEq(name, len, "OSL.APP.NAME"))
        {
            fixed (char* p = "OSLANG") return OsStr(p, 6);
        }
        if (OsNameEq(name, len, "OSL.APP.EXIT"))
        {
            _osExit = true;
            return OsNull();
        }

        // ---- standard functions ----
        if (OsNameEq(name, len, "NUMBER"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            if (a.K == VKNum) return a;
            if (a.K == VKBool) return OsNum(a.B ? 1 : 0);
            if (a.K == VKStr)
            {
                char* buf = stackalloc char[64];
                int n = OsValToBuf(a, buf, 64);
                return OsNum(OsParseNumber(buf, n));
            }
            return OsNum(0);
        }
        if (OsNameEq(name, len, "STR"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            return OsToDisplayStr(a);
        }
        if (OsNameEq(name, len, "BOOL"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            return OsBool(OsTruthy(a));
        }
        if (OsNameEq(name, len, "ABS"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (a.K != VKNum) { OsFail("ABS requires a number."); return OsNull(); }
            return OsNum(OsAbs(a.D));
        }
        if (OsNameEq(name, len, "SQR") || OsNameEq(name, len, "SQRT"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (a.K != VKNum) { OsFail("SQRT requires a number."); return OsNull(); }
            return OsNum(OsSqrt(a.D));
        }
        if (OsNameEq(name, len, "FLOOR"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (a.K != VKNum) { OsFail("FLOOR requires a number."); return OsNull(); }
            return OsNum(OsFloor(a.D));
        }
        if (OsNameEq(name, len, "CEIL"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (a.K != VKNum) { OsFail("CEIL requires a number."); return OsNull(); }
            return OsNum(-OsFloor(-a.D));
        }
        if (OsNameEq(name, len, "POW"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (a.K != VKNum || b.K != VKNum) { OsFail("POW requires two numbers."); return OsNull(); }
            return OsNum(OsPow(a.D, b.D));
        }
        if (OsNameEq(name, len, "MOD"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (a.K != VKNum || b.K != VKNum) { OsFail("MOD requires two numbers."); return OsNull(); }
            if (b.D == 0) { OsFail("Division by zero."); return OsNull(); }
            return OsNum(a.D - OsFloor(a.D / b.D) * b.D);
        }
        if (OsNameEq(name, len, "MAX"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (a.K != VKNum || b.K != VKNum) { OsFail("MAX requires two numbers."); return OsNull(); }
            return OsNum(a.D > b.D ? a.D : b.D);
        }
        if (OsNameEq(name, len, "MIN"))
        {
            OslVal a = OsArg(args, argc, 0);
            OslVal b = OsArg(args, argc, 1);
            if (a.K != VKNum || b.K != VKNum) { OsFail("MIN requires two numbers."); return OsNull(); }
            return OsNum(a.D < b.D ? a.D : b.D);
        }
        if (OsNameEq(name, len, "LEN") || OsNameEq(name, len, "COUNT"))
        {
            OslVal a = OsArg(args, argc, 0);
            if (_osErr) return OsNull();
            if (a.K == VKStr) return OsNum(a.L);
            if (a.K == VKArr) return OsNum(OsArrCount(a));
            OsFail("LEN/COUNT requires a string or array.");
            return OsNull();
        }
        if (OsNameEq(name, len, "TYPEOF"))
        {
            OslVal a = OsArg(args, argc, 0);
            switch (a.K)
            {
                case VKNull: fixed (char* p = "NULL") return OsStr(p, 4);
                case VKBool: fixed (char* p = "BOOLEAN") return OsStr(p, 7);
                case VKNum: fixed (char* p = "NUMBER") return OsStr(p, 6);
                case VKStr: fixed (char* p = "STRING") return OsStr(p, 6);
                case VKArr: fixed (char* p = "ARRAY") return OsStr(p, 5);
                case VKObj:
                {
                    byte* mem = (byte*)a.P;
                    int classId = *(int*)mem;
                    if (classId >= 0 && classId < _osClassCount)
                    {
                        ClassDef* cls = &_osClasses[classId];
                        char* clsName = cls->N;
                        int nameLen = cls->NL;
                        char* buf = (char*)OsAlloc((nameLen + 1) * 2);
                        for (int i = 0; i < nameLen; i++) buf[i] = clsName[i];
                        buf[nameLen] = '\0';
                        return OsStr(buf, nameLen);
                    }
                    if (classId == -1)
                    {
                        fixed (char* p = "KEYBOARDEVENT") return OsStr(p, 13);
                    }
                    fixed (char* p = "OBJECT") return OsStr(p, 6);
                }
            }
            fixed (char* p = "UNKNOWN") return OsStr(p, 7);
        }

        OsFailName(name, len, "Unknown function '");
        return OsNull();
    }

    // ---- OSL.FILE helpers (read side, ext4) ----
    private static bool OsPathToIno(OslVal path, out uint ino)
    {
        ino = 0;
        if (path.K != VKStr) { OsFail("Expected a path string."); return false; }
        char* buf = stackalloc char[300];
        int n = OsValToBuf(path, buf, 300);
        if (!Ext4.Resolve(buf, n, out ino)) return false;
        return true;
    }

    private static bool OsFileExists(OslVal path)
    {
        uint ino;
        if (!OsPathToIno(path, out ino)) return false;
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) return false;
        return Ext4.IsReg(Ext4.InodeGetMode(cin)) || Ext4.IsDir(Ext4.InodeGetMode(cin));
    }

    private static double OsFileSize(OslVal path)
    {
        uint ino;
        if (!OsPathToIno(path, out ino)) return 0;
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) return 0;
        return (double)Ext4.InodeGetSize(cin);
    }

    private static OslVal OsFileRead(OslVal path)
    {
        uint ino;
        if (!OsPathToIno(path, out ino)) return OsStr(null, 0);
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) return OsStr(null, 0);
        if (!Ext4.IsReg(Ext4.InodeGetMode(cin))) { OsFail("Not a regular file."); return OsStr(null, 0); }
        uint sz = Ext4.InodeGetSize(cin);
        // read into the arena (as chars)
        char* buf = (char*)OsAlloc((int)sz * 2 + 2);
        byte* blk = stackalloc byte[Ext4.BlockSize];
        ulong off = 0;
        int dst = 0;
        while (off < (ulong)sz)
        {
            ulong lblk = off / (ulong)Ext4.BlockSize;
            int boff = (int)(off % (ulong)Ext4.BlockSize);
            uint bnum = Ext4.InodeGetBlock(cin, lblk);
            if (bnum == 0) break;
            Ext4.ReadBlock(bnum, blk);
            int take = Ext4.BlockSize - boff;
            if ((ulong)(off + (ulong)take) > (ulong)sz) take = (int)((ulong)sz - off);
            for (int k = 0; k < take; k++) buf[dst++] = (char)blk[boff + k];
            off += (ulong)take;
        }
        OslVal v = default;
        v.K = VKStr; v.P = (ulong)buf; v.L = dst;
        return v;
    }

    // ==================================================================
    //  INPUT (INPUT statement / OSL.CONSOLE.INPUT)
    // ==================================================================
    private static int OsReadLine(char* buf, int max)
    {
        int len = 0;
        bool shift = false;
        while (true)
        {
            if (ConsoleControl.TakeCtrlC()) { Vga.PutChar('\n'); buf[len] = '\0'; return len; }
            int ch = Serial.Pop();
            if (ch >= 0)
            {
                char c = (char)ch;
                if (c == '\r' || c == '\n') { Vga.PutChar('\n'); buf[len] = '\0'; return len; }
                if (c == '\b' || c == 0x7F)
                {
                    if (len > 0) { len--; Vga.PutChar('\b'); Vga.PutChar(' '); Vga.PutChar('\b'); }
                    continue;
                }
                if (c >= ' ' && c < 127 && len < max - 1) { buf[len++] = c; Vga.PutChar(c); }
                continue;
            }
            int sc = Keyboard.Pop();
            if (sc < 0) { NativeMethods.halt(); continue; }
            if (sc == 0x2A || sc == 0x36) { shift = true; continue; }
            if (sc == 0xAA || sc == 0xB6) { shift = false; continue; }
            if ((sc & 0x80) != 0) continue;
            if (sc == 0x1C) { Vga.PutChar('\n'); buf[len] = '\0'; return len; } // Enter
            if (sc == 0x0E) // Backspace
            {
                if (len > 0) { len--; Vga.PutChar('\b'); Vga.PutChar(' '); Vga.PutChar('\b'); }
                continue;
            }
            char kc = Keyboard.MapScancode((byte)sc, shift);
            if (kc != 0 && kc >= ' ' && kc < 127 && len < max - 1) { buf[len++] = kc; Vga.PutChar(kc); }
        }
    }

    // ==================================================================
    //  OSLANG shell command entry point
    // ==================================================================
    // Called by the shell for: OSLANG <script.osl> [args...]
    public static void OslangCmd(char* line, int len)
    {
        // Skip the command word itself (do not assume its length).
        int i = 0;
        while (i < len && line[i] != ' ' && line[i] != '\t') i++;
        while (i < len && (line[i] == ' ' || line[i] == '\t')) i++;
        int pathS = i;
        while (i < len && line[i] != ' ' && line[i] != '\t') i++;
        int pathE = i;
        if (pathS >= pathE) { Vga.WriteLine("Usage: OSLANG <script.osl> [args]"); return; }
        int argS = i;
        while (argS < len && (line[argS] == ' ' || line[argS] == '\t')) argS++;
        int argLen = len - argS;

        _osScriptPath = line + pathS;
        _osScriptPathLen = pathE - pathS;

        // One-time arena allocation (4 MiB, frame-allocated, identity-mapped).
        if (_osHeap == null)
        {
            ulong frames = PhysicalMemory.Alloc(1024, 0x1000);
            if (frames == 0) { Vga.WriteLine("Out of memory for OSLANG."); return; }
            _osHeap = (byte*)frames;
            _osHeapSize = 1024UL * 0x1000;
        }
        // The interpreter's own tables, token array and token sub-buffers are
        // allocated from the same arena, so the script itself only gets a slice.
        _osHeapUsed = 0;
        _osErr = false;
        _osFuncCount = 0;
        _osClassCount = 0;
        _osEnumCount = 0;
        _osCurrentClassId = -1;
        _osCurrentEnumId = -1;
        _osCtl = CTL_NONE;
        _osExit = false;
        _osCallDepth = 0;

        // Initialize class, enum, and interface tables
        _osClasses = (ClassDef*)OsAlloc(OS_MAX_CLASSES * sizeof(ClassDef));
        _osEnums = (EnumDef*)OsAlloc(OS_MAX_ENUMS * sizeof(EnumDef));
        _osInterfaces = (InterfaceDef*)OsAlloc(OS_MAX_INTERFACES * sizeof(InterfaceDef));

        // Define built-in KEY enum
        OsDefineKeyEnum();
        OsDefineKeyCodeEnum();

        // Load the script into the arena after the built-in definitions. The cap is
        // expressed in ARENA BYTES: the source is UTF-16, so a script of N chars
        // needs 2N bytes. The old cap was _osHeapSize/2 chars against a byte arena,
        // which let a ~1 MiB script write past the end (B6).
        char* src = (char*)(_osHeap + _osHeapUsed);
        int srcCap = (int)(MaxScriptBytes / 2);
        bool truncated;
        int srcLen = OslangLoadFile(line + pathS, pathE - pathS, src, srcCap, out truncated);
        if (srcLen < 0) { Vga.WriteLine("File not found."); _osHeapUsed = 0; return; }
        if (truncated)
        {
            Vga.Write("OSLANG error: script larger than ");
            Vga.WriteDec(MaxScriptBytes / 1024UL);            // KiB
            Vga.WriteLine(" KiB; not run.");
            _osHeapUsed = 0;
            return;
        }
        ulong srcBytes = (ulong)(((srcLen + 1) * 2 + 7) & ~7);
        if (_osHeapUsed + srcBytes > _osHeapSize)
        {
            Vga.WriteLine("OSLANG error: script too large for the arena.");
            _osHeapUsed = 0;
            return;
        }
        _osHeapUsed += srcBytes;

        // Args live in the command-line buffer (valid for this call).
        _osArgs = line + argS;
        _osArgsLen = argLen;

        // Global scope + function table.
        Scope* gs = (Scope*)OsAlloc(sizeof(Scope));
        gs->E = (VarEntry*)OsAlloc(16 * sizeof(VarEntry));
        gs->C = 0; gs->Cap = 16; gs->Parent = 0;
        _osGlobalScope = (ulong)gs;
        _osScope = _osGlobalScope;
        _osFuncs = (FuncDef*)OsAlloc(OS_MAX_FUNCS * sizeof(FuncDef));
        _osFuncCount = 0;

        // Lex.
        int maxToks = 16384;
        _osToks = (Tok*)OsAlloc(maxToks * sizeof(Tok));
        _osTokCount = OsLex(src, srcLen, _osToks, maxToks);
        _osPos = 0;
        if (_osErr) { _osHeapUsed = 0; return; }

        // Parse.
        int topCount;
        Stmt* top = OsParseProgram(out topCount);
        if (_osErr) { _osHeapUsed = 0; return; }

        // Expose the command-line arguments as a global ARGS array, matching the
        // OSLANG application convention used by scripts such as KISS.
        {
            OslVal argsArr = OsArrMake(4);
            int s = 0;
            while (s < _osArgsLen && _osArgs[s] == ' ') s++;
            int e = _osArgsLen;
            while (e > s && _osArgs[e - 1] == ' ') e--;
            if (e > s)
            {
                int al = e - s;
                char* ab = (char*)OsAlloc((al + 1) * 2);
                for (int k = 0; k < al; k++) ab[k] = _osArgs[s + k];
                ab[al] = '\0';
                OsArrSetCount(argsArr, 1);
                OsArrElem(argsArr)[0] = OsStr(ab, al);
            }
            fixed (char* argName = "ARGS") OsDeclareGlobal(argName, 4, argsArr);
        }

        // Run the program: top-level statements first (so a top-level GLOBAL is
        // defined before MAIN reads it), then MAIN() if the program has one.
        // A program without MAIN is just its top-level statements.
        OsExecBlock(top, topCount);
        fixed (char* mn = "MAIN")
        {
            FuncDef* main = OsFindFunc(mn, 4);
            if (main != null) OsCallFunction(main, null, 0);
        }
        _osHeapUsed = 0; // reclaim the whole arena for the next run
    }

    private static void OsDefineKeyEnum()
    {
        if (_osEnumCount >= OS_MAX_ENUMS) return;
        
        EnumDef* e = &_osEnums[_osEnumCount++];
        
        // Set enum name "KEY" directly in arena (UTF-16: 3 chars + null = 8 bytes)
        char* keyName = (char*)OsAlloc(8);
        keyName[0] = 'K'; keyName[1] = 'E'; keyName[2] = 'Y'; keyName[3] = '\0';
        e->N = keyName;
        e->NL = 3;

        // Built once per process; the table below is the single source of truth, so the
        // member count can never disagree with the number of entries (the old
        // hand-written table declared 35 slots and filled 28, leaving wild
        // pointers for four loops to dereference - B5).
        if (_osKeyMembersReady == 0)
        {
            OsInitKeyMembers();
            _osKeyMembersReady = 1;
        }

        const int memberCount = KeyMemberCount;
        e->Members = (char**)OsAlloc(memberCount * sizeof(char*));
        e->MemberLens = (int*)OsAlloc(memberCount * sizeof(int));
        e->Values = (ulong*)OsAlloc(memberCount * sizeof(ulong));
        e->MemberCount = memberCount;
        e->IsFlags = false;

        for (int i = 0; i < memberCount; i++)
        {
            int n = _keyMembers.NameLens[i];
            char* buf = (char*)OsAlloc((n + 1) * 2);
            fixed (char* names = _keyMembers.Names)
            {
                char* slot = names + (ulong)i * KeyNameCap;
                for (int k = 0; k < n; k++) buf[k] = slot[k];
            }
            buf[n] = '\0';
            e->Members[i] = buf;
            e->MemberLens[i] = n;
            e->Values[i] = _keyMembers.Values[i];
        }
    }

    // Built-in KEY enum members. Values are PS/2 set-1 scancodes so that
    // OSL.CONSOLE.GETKEY() can map a key directly to its enum value; the spec
    // leaves the numeric values implementation-defined. Stored as fixed buffers
    // rather than a static array: the freestanding AOT runtime has no support for
    // managed array data (the same reason TaskTable/EnvTable are fixed structs).
    private const int KeyMemberCount = 28;
    private const int KeyNameCap = 16;      // "BACKSPACE" + NUL, in UTF-16

    // Flat buffers: a `fixed` array of structs is not allowed in C#.
    private unsafe struct KeyMemberTable
    {
        public fixed char Names[KeyMemberCount * KeyNameCap];
        public fixed int NameLens[KeyMemberCount];
        public fixed ulong Values[KeyMemberCount];
    }

    private static KeyMemberTable _keyMembers;
    private static int _osKeyMembersReady;

    private static void OsInitKeyMembers()
    {
        //          name            len  value
        OsKeyMember(0,  "UNKNOWN",   7, 0x00);
        OsKeyMember(1,  "ENTER",     5, 0x1C);
        OsKeyMember(2,  "ESC",       3, 0x01);
        OsKeyMember(3,  "TAB",       3, 0x0F);
        OsKeyMember(4,  "BACKSPACE", 9, 0x0E);
        OsKeyMember(5,  "DELETE",    6, 0x53);
        OsKeyMember(6,  "INSERT",    6, 0x52);
        OsKeyMember(7,  "SPACE",     5, 0x39);
        OsKeyMember(8,  "UP",        2, 0x48);
        OsKeyMember(9,  "DOWN",      4, 0x50);
        OsKeyMember(10, "LEFT",      4, 0x4B);
        OsKeyMember(11, "RIGHT",     5, 0x4D);
        OsKeyMember(12, "HOME",      4, 0x47);
        OsKeyMember(13, "END",       3, 0x4F);
        OsKeyMember(14, "PAGEUP",    6, 0x49);
        OsKeyMember(15, "PAGEDOWN",  8, 0x51);
        OsKeyMember(16, "F1",        2, 0x3B);
        OsKeyMember(17, "F2",        2, 0x3C);
        OsKeyMember(18, "F3",        2, 0x3D);
        OsKeyMember(19, "F4",        2, 0x3E);
        OsKeyMember(20, "F5",        2, 0x3F);
        OsKeyMember(21, "F6",        2, 0x40);
        OsKeyMember(22, "F7",        2, 0x41);
        OsKeyMember(23, "F8",        2, 0x42);
        OsKeyMember(24, "F9",        2, 0x43);
        OsKeyMember(25, "F10",       3, 0x44);
        OsKeyMember(26, "F11",       3, 0x57);
        OsKeyMember(27, "F12",       3, 0x58);
    }

    private static void OsKeyMember(int index, string name, int len, ulong value)
    {
        _keyMembers.NameLens[index] = len;
        _keyMembers.Values[index] = value;
        fixed (char* dst = _keyMembers.Names)
        fixed (char* src = name)
        {
            char* slot = dst + (ulong)index * KeyNameCap;
            for (int i = 0; i < len && i < KeyNameCap - 1; i++) slot[i] = src[i];
            slot[len] = '\0';
        }
    }

    // Read a whole file from A: into a char buffer; returns length or -1.
    // Returns the number of chars read, or -1 if the file cannot be read. A file
    // larger than `maxChars` is truncated and `truncated` is set, so the caller
    // can refuse to run a half-loaded script instead of failing obscurely (B6).
    private static int OslangLoadFile(char* path, int pathLen, char* dst, int maxChars, out bool truncated)
    {
        truncated = false;
        uint ino;
        if (!Ext4.Resolve(path, pathLen, out ino)) return -1;
        byte* cin = stackalloc byte[256];
        if (!Ext4.ReadInode(ino, cin)) return -1;
        if (!Ext4.IsReg(Ext4.InodeGetMode(cin))) return -1;
        uint sz = Ext4.InodeGetSize(cin);
        int total = (int)sz;
        if (total > maxChars) { total = maxChars; truncated = true; }
        byte* blk = stackalloc byte[Ext4.BlockSize];
        ulong off = 0;
        int dstOff = 0;
        while (off < (ulong)total)
        {
            ulong lblk = off / (ulong)Ext4.BlockSize;
            int boff = (int)(off % (ulong)Ext4.BlockSize);
            uint bnum = Ext4.InodeGetBlock(cin, lblk);
            if (bnum == 0) break;
            Ext4.ReadBlock(bnum, blk);
            int take = Ext4.BlockSize - boff;
            if ((ulong)(off + (ulong)take) > (ulong)total) take = (int)((ulong)total - off);
            for (int k = 0; k < take; k++) dst[dstOff++] = (char)blk[boff + k];
            off += (ulong)take;
        }
        return dstOff;
    }
}
