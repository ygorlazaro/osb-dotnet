// Methods called on primitive values: "abc".TOUPPER(), 42.ISINTEGER(),
// [1,2,3].JOIN("-").
//
// Split out of OslangKernel.cs (which is one partial class of Kernel) purely to
// keep the file navigable. The dispatcher is reached from OsEvalCall when the
// callee is a member access whose receiver is a STRING, NUMBER, BOOLEAN or
// ARRAY - class instances go to OsEvalCall's object-method path instead.
//
// Callback-taking methods (MAP/FILTER/ANY/FOREACH/REDUCE/...) are NOT here:
// the kernel has no first-class function values yet, so there is nothing to
// pass. They arrive with arrow functions in a later phase.

internal static unsafe partial class Kernel
{
    // Argument at `i`, or NULL when the call passed fewer arguments.
    private static OslVal OsMArg(Expr** args, int argc, int i)
    {
        if (i < argc) return OsEval(args[i]);
        return OsNull();
    }

    private static char* OsMArgStr(Expr** args, int argc, int i, out int len)
    {
        OslVal v = OsMArg(args, argc, i);
        len = 0;
        if (v.K != VKStr) return null;
        len = v.L;
        return (char*)v.P;
    }

    private static double OsMArgNum(Expr** args, int argc, int i, double fallback)
    {
        OslVal v = OsMArg(args, argc, i);
        if (_osErr) return fallback;
        return v.K == VKNum ? v.D : fallback;
    }

    // Append in place: the array grows through its header, so the caller's
    // variable sees the new element without any assignment.
    private static OslVal OsArrWith(OslVal arr, OslVal item)
    {
        OsArrEnsure(arr, 1);
        if (_osErr) return OsNull();
        int count = OsArrCount(arr);
        OsArrSetCount(arr, count + 1);
        OsArrElem(arr)[count] = item;
        return arr;
    }

    private static OslVal OsMakeStr(char* buf, int len)
    {
        return OsStr(buf, len);
    }

    // ---- STRING ------------------------------------------------------------

    private static OslVal OsStrMethod(OslVal recv, char* name, int nlen, Expr** args, int argc)
    {
        char* s = (char*)recv.P;
        int n = recv.L;

        if (OsNameEq(name, nlen, "TOUPPER") || OsNameEq(name, nlen, "TOLOWER"))
        {
            bool up = OsNameEq(name, nlen, "TOUPPER");
            char* buf = (char*)OsAlloc(n * 2 + 2);
            if (buf == null) return OsNull();
            for (int i = 0; i < n; i++)
            {
                char c = s[i];
                if (c >= 'a' && c <= 'z') c = up ? (char)(c - 32) : c;
                else if (c >= 'A' && c <= 'Z' && !up) c = (char)(c + 32);
                buf[i] = c;
            }
            return OsMakeStr(buf, n);
        }

        if (OsNameEq(name, nlen, "TRIM"))
        {
            int a = 0, b = n;
            while (a < b && (s[a] == ' ' || s[a] == '\t')) a++;
            while (b > a && (s[b - 1] == ' ' || s[b - 1] == '\t')) b--;
            char* buf = (char*)OsAlloc((b - a) * 2 + 2);
            if (buf == null) return OsNull();
            for (int i = a; i < b; i++) buf[i - a] = s[i];
            return OsMakeStr(buf, b - a);
        }

        if (OsNameEq(name, nlen, "LENGTH") || OsNameEq(name, nlen, "COUNT")) return OsNum(n);
        if (OsNameEq(name, nlen, "ISEMPTY")) return OsBool(n == 0);
        if (OsNameEq(name, nlen, "TOSTRING")) return recv;

        if (OsNameEq(name, nlen, "STARTSWITH") || OsNameEq(name, nlen, "ENDSWITH") ||
            OsNameEq(name, nlen, "CONTAINS"))
        {
            int want;
            char* w = OsMArgStr(args, argc, 0, out want);
            if (_osErr) return OsNull();
            if (w == null) return OsBool(false);
            if (OsNameEq(name, nlen, "CONTAINS")) return OsBool(OsStrFind(s, n, w, want) >= 0);
            if (want > n) return OsBool(false);
            if (OsNameEq(name, nlen, "STARTSWITH"))
            {
                for (int i = 0; i < want; i++) if (s[i] != w[i]) return OsBool(false);
                return OsBool(true);
            }
            for (int i = 0; i < want; i++) if (s[n - want + i] != w[i]) return OsBool(false);
            return OsBool(true);
        }

        if (OsNameEq(name, nlen, "INDEXOF") || OsNameEq(name, nlen, "FIND"))
        {
            int want;
            char* w = OsMArgStr(args, argc, 0, out want);
            if (_osErr) return OsNull();
            if (w == null) return OsNum(-1);
            return OsNum(OsStrFind(s, n, w, want));
        }

        if (OsNameEq(name, nlen, "SUBSTR"))
        {
            int start = (int)OsMArgNum(args, argc, 0, 0);
            int len = (int)OsMArgNum(args, argc, 1, n - start);
            if (_osErr) return OsNull();
            if (start < 0) start = 0;
            if (start > n) start = n;
            if (len < 0) len = 0;
            if (start + len > n) len = n - start;
            char* buf = (char*)OsAlloc(len * 2 + 2);
            if (buf == null) return OsNull();
            for (int i = 0; i < len; i++) buf[i] = s[start + i];
            return OsMakeStr(buf, len);
        }

        if (OsNameEq(name, nlen, "LEFT") || OsNameEq(name, nlen, "RIGHT"))
        {
            int want = (int)OsMArgNum(args, argc, 0, 0);
            if (_osErr) return OsNull();
            if (want < 0) want = 0;
            if (want > n) want = n;
            int off = OsNameEq(name, nlen, "LEFT") ? 0 : n - want;
            char* buf = (char*)OsAlloc(want * 2 + 2);
            if (buf == null) return OsNull();
            for (int i = 0; i < want; i++) buf[i] = s[off + i];
            return OsMakeStr(buf, want);
        }

        if (OsNameEq(name, nlen, "REPLACE"))
        {
            int fl;
            char* from = OsMArgStr(args, argc, 0, out fl);
            if (_osErr) return OsNull();
            int tl;
            char* to = OsMArgStr(args, argc, 1, out tl);
            if (_osErr) return OsNull();
            if (from == null || to == null || fl == 0) return recv;
            if (OsStrFind(s, n, from, fl) < 0) return recv;
            int worst = n * tl + 2;
            char* buf = (char*)OsAlloc(worst * 2 + 2);
            if (buf == null) return OsNull();
            int o = 0;
            int i = 0;
            while (i < n)
            {
                if (i + fl <= n)
                {
                    bool same = true;
                    for (int k = 0; k < fl; k++) if (s[i + k] != from[k]) { same = false; break; }
                    if (same)
                    {
                        for (int k = 0; k < tl; k++) buf[o++] = to[k];
                        i += fl;
                        continue;
                    }
                }
                buf[o++] = s[i++];
            }
            return OsMakeStr(buf, o);
        }

        if (OsNameEq(name, nlen, "REPEAT"))
        {
            int times = (int)OsMArgNum(args, argc, 0, 0);
            if (_osErr) return OsNull();
            if (times < 0) times = 0;
            char* buf = (char*)OsAlloc(n * times * 2 + 2);
            if (buf == null) return OsNull();
            int o = 0;
            for (int t = 0; t < times; t++) for (int i = 0; i < n; i++) buf[o++] = s[i];
            return OsMakeStr(buf, o);
        }

        if (OsNameEq(name, nlen, "PADSTART") || OsNameEq(name, nlen, "PADEND"))
        {
            int width = (int)OsMArgNum(args, argc, 0, 0);
            if (_osErr) return OsNull();
            if (width <= n) return recv;
            int pl = 1;
            char* pad = OsMArgStr(args, argc, 1, out pl);
            if (_osErr) return OsNull();
            char padChar = pad != null && pl > 0 ? pad[0] : ' ';
            char* buf = (char*)OsAlloc(width * 2 + 2);
            if (buf == null) return OsNull();
            bool start = OsNameEq(name, nlen, "PADSTART");
            for (int i = 0; i < width; i++)
            {
                bool padPos = start ? (i < width - n) : (i >= n);
                buf[i] = padPos ? padChar : s[i - (start ? width - n : 0)];
            }
            return OsMakeStr(buf, width);
        }

        if (OsNameEq(name, nlen, "REVERSE"))
        {
            char* buf = (char*)OsAlloc(n * 2 + 2);
            if (buf == null) return OsNull();
            for (int i = 0; i < n; i++) buf[i] = s[n - 1 - i];
            return OsMakeStr(buf, n);
        }

        if (OsNameEq(name, nlen, "SPLIT"))
        {
            int sl;
            char* sep = OsMArgStr(args, argc, 0, out sl);
            if (_osErr) return OsNull();
            if (sep == null || sl == 0) return OsArrWith(OsArrMake(1), recv);

            OslVal parts = OsArrMake(n + 1);
            if (parts.K != VKArr) return OsNull();
            int start = 0;
            int i = 0;
            while (i <= n - sl)
            {
                bool same = true;
                for (int k = 0; k < sl; k++) if (s[i + k] != sep[k]) { same = false; break; }
                if (same)
                {
                    int piece = i - start;
                    char* piece2 = (char*)OsAlloc(piece * 2 + 2);
                    if (piece2 == null) return OsNull();
                    for (int k = 0; k < piece; k++) piece2[k] = s[start + k];
                    OsArrSetCount(parts, OsArrCount(parts) + 1);
                    OsArrElem(parts)[OsArrCount(parts) - 1] = OsMakeStr(piece2, piece);
                    i += sl;
                    start = i;
                    continue;
                }
                i++;
            }
            int tail = n - start;
            char* last = (char*)OsAlloc(tail * 2 + 2);
            if (last == null) return OsNull();
            for (int k = 0; k < tail; k++) last[k] = s[start + k];
            OsArrSetCount(parts, OsArrCount(parts) + 1);
            OsArrElem(parts)[OsArrCount(parts) - 1] = OsMakeStr(last, tail);
            return parts;
        }

        if (OsNameEq(name, nlen, "INSERT"))
        {
            int idx = (int)OsMArgNum(args, argc, 0, 0);
            int tl;
            char* t = OsMArgStr(args, argc, 1, out tl);
            if (_osErr) return OsNull();
            if (idx < 0) idx = 0;
            if (idx > n) idx = n;
            char* buf = (char*)OsAlloc((n + tl + 1) * 2 + 2);
            if (buf == null) return OsNull();
            int o = 0;
            for (int i = 0; i < idx; i++) buf[o++] = s[i];
            for (int i = 0; i < tl; i++) buf[o++] = t[i];
            for (int i = idx; i < n; i++) buf[o++] = s[i];
            return OsMakeStr(buf, o);
        }

        if (OsNameEq(name, nlen, "REMOVE"))
        {
            int start = (int)OsMArgNum(args, argc, 0, 0);
            int rlen = (int)OsMArgNum(args, argc, 1, 0);
            if (_osErr) return OsNull();
            if (start < 0) start = 0;
            if (start > n) start = n;
            if (rlen < 0) rlen = 0;
            if (start + rlen > n) rlen = n - start;
            char* buf = (char*)OsAlloc((n - rlen + 1) * 2 + 2);
            if (buf == null) return OsNull();
            int o = 0;
            for (int i = 0; i < start; i++) buf[o++] = s[i];
            for (int i = start + rlen; i < n; i++) buf[o++] = s[i];
            return OsMakeStr(buf, o);
        }

        OsFailName(name, nlen, "Unknown STRING method '");
        return OsNull();
    }

    // Case-sensitive substring search; -1 when absent.
    private static int OsStrFind(char* hay, int hn, char* needle, int nn)
    {
        if (nn == 0) return 0;
        if (nn > hn) return -1;
        for (int i = 0; i <= hn - nn; i++)
        {
            bool same = true;
            for (int k = 0; k < nn; k++) if (hay[i + k] != needle[k]) { same = false; break; }
            if (same) return i;
        }
        return -1;
    }

    // ---- NUMBER ------------------------------------------------------------

    private static OslVal OsNumMethod(OslVal recv, char* name, int nlen, Expr** args, int argc)
    {
        double v = recv.D;

        if (OsNameEq(name, nlen, "TOSTRING")) return OsToDisplayStr(recv);
        if (OsNameEq(name, nlen, "ABS")) return OsNum(OsAbs(v));
        if (OsNameEq(name, nlen, "FLOOR")) return OsNum(OsFloor(v));
        if (OsNameEq(name, nlen, "CEIL")) return OsNum(v == OsFloor(v) ? v : OsFloor(v) + 1);
        if (OsNameEq(name, nlen, "ISINTEGER")) return OsBool(v == OsFloor(v));
        if (OsNameEq(name, nlen, "BETWEEN"))
        {
            double lo = OsMArgNum(args, argc, 0, v);
            double hi = OsMArgNum(args, argc, 1, v);
            if (_osErr) return OsNull();
            return OsBool(v >= lo && v <= hi);
        }
        if (OsNameEq(name, nlen, "TRUNC"))
        {
            double digits = OsMArgNum(args, argc, 0, 0);
            if (_osErr) return OsNull();
            if (digits <= 0) return OsNum(v < 0 ? -OsFloor(-v) : OsFloor(v));
            double scale = OsPowInt(10, (long)digits);
            double scaled = v * scale;
            double t = scaled < 0 ? -OsFloor(-scaled) : OsFloor(scaled);
            return OsNum(t / scale);
        }

        OsFailName(name, nlen, "Unknown NUMBER method '");
        return OsNull();
    }

    // ---- BOOLEAN -----------------------------------------------------------

    private static OslVal OsBoolMethod(OslVal recv, char* name, int nlen)
    {
        if (OsNameEq(name, nlen, "TOSTRING"))
        {
            char* buf = OsCopyStr(recv.B ? "TRUE" : "FALSE");
            if (buf == null) return OsNull();
            return OsStr(buf, recv.B ? 4 : 5);
        }
        if (OsNameEq(name, nlen, "TOGGLE")) return OsBool(!recv.B);
        OsFailName(name, nlen, "Unknown BOOLEAN method '");
        return OsNull();
    }

    // Copy a literal into the arena as a NUL-terminated string.
    private static char* OsCopyStr(string s)
    {
        char* buf = (char*)OsAlloc((s.Length + 1) * 2);
        if (buf == null) return null;
        fixed (char* src = s)
        {
            for (int i = 0; i < s.Length; i++) buf[i] = src[i];
        }
        buf[s.Length] = '\0';
        return buf;
    }

    // ---- ARRAY -------------------------------------------------------------

    private static OslVal OsArrMethod(OslVal recv, char* name, int nlen, Expr** args, int argc)
    {
        int count = OsArrCount(recv);
        OslVal* elems = OsArrElem(recv);

        if (OsNameEq(name, nlen, "COUNT")) return OsNum(count);
        if (OsNameEq(name, nlen, "FIRST")) return count > 0 ? elems[0] : OsNull();
        if (OsNameEq(name, nlen, "LAST")) return count > 0 ? elems[count - 1] : OsNull();

        if (OsNameEq(name, nlen, "CONTAINS") || OsNameEq(name, nlen, "INDEXOF"))
        {
            OslVal want = OsMArg(args, argc, 0);
            if (_osErr) return OsNull();
            int at = -1;
            for (int i = 0; i < count; i++)
                if (OsValuesEqual(elems[i], want)) { at = i; break; }
            return OsNameEq(name, nlen, "CONTAINS") ? OsBool(at >= 0) : OsNum(at);
        }

        if (OsNameEq(name, nlen, "ADD") || OsNameEq(name, nlen, "PUSH"))
        {
            OslVal item = OsMArg(args, argc, 0);
            if (_osErr) return OsNull();
            OslVal grown = OsArrWith(recv, item);
            if (grown.K != VKArr) return OsNull();
            return grown;
        }

        if (OsNameEq(name, nlen, "POP"))
        {
            if (count == 0) return OsNull();
            OslVal last = elems[count - 1];
            OsArrSetCount(recv, count - 1);
            return last;
        }

        if (OsNameEq(name, nlen, "CLEAR"))
        {
            OsArrSetCount(recv, 0);
            return OsNull();
        }

        if (OsNameEq(name, nlen, "REMOVE"))
        {
            OslVal want = OsMArg(args, argc, 0);
            if (_osErr) return OsNull();
            for (int i = 0; i < count; i++)
            {
                if (!OsValuesEqual(elems[i], want)) continue;
                for (int k = i; k < count - 1; k++) elems[k] = elems[k + 1];
                OsArrSetCount(recv, count - 1);
                return want;
            }
            OsFail("Value not found in array.");
            return OsNull();
        }

        if (OsNameEq(name, nlen, "REVERSE"))
        {
            for (int i = 0; i < count / 2; i++)
            {
                OslVal t = elems[i];
                elems[i] = elems[count - 1 - i];
                elems[count - 1 - i] = t;
            }
            return recv;
        }

        if (OsNameEq(name, nlen, "SORT"))
        {
            // Insertion sort, ascending: shift while the element before the
            // insertion point is greater than the key.
            for (int i = 1; i < count; i++)
            {
                OslVal key = elems[i];
                int j = i - 1;
                while (j >= 0 && OsSortLess(key, elems[j]))
                {
                    elems[j + 1] = elems[j];
                    j--;
                }
                elems[j + 1] = key;
            }
            return recv;
        }

        if (OsNameEq(name, nlen, "JOIN"))
        {
            int sl;
            char* sep = OsMArgStr(args, argc, 0, out sl);
            if (_osErr) return OsNull();
            char sepCh = sep != null && sl > 0 ? sep[0] : '\0';
            int total = 0;
            for (int i = 0; i < count; i++) total += OsDisplayLen(elems[i]);
            total += (count - 1) * (sl > 0 ? sl : 0);
            if (total < 0) total = 0;
            char* buf = (char*)OsAlloc(total * 2 + 2);
            if (buf == null) return OsNull();
            int o = 0;
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && sl > 0) for (int k = 0; k < sl; k++) buf[o++] = sep[k];
                o += OsWriteDisplay(buf + o, elems[i]);
            }
            return OsMakeStr(buf, o);
        }

        // ---- callback methods (0.51) --------------------------------------
        if (OsNameEq(name, nlen, "MAP") || OsNameEq(name, nlen, "FILTER") ||
            OsNameEq(name, nlen, "ANY") || OsNameEq(name, nlen, "SOME") ||
            OsNameEq(name, nlen, "ALL") || OsNameEq(name, nlen, "FINDINDEX") ||
            OsNameEq(name, nlen, "FOREACH") || OsNameEq(name, nlen, "REDUCE") ||
            OsNameEq(name, nlen, "FLATMAP"))
        {
            OslVal fn = OsMArg(args, argc, 0);
            if (_osErr) return OsNull();
            if (fn.K != VKFunc)
            {
                OsFailNeedsFunction(name, nlen);
                return OsNull();
            }

            if (OsNameEq(name, nlen, "MAP"))
            {
                OslVal out2 = OsArrMake(count);
                if (out2.K != VKArr) return OsNull();
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OslVal r = OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                    OsArrSetCount(out2, OsArrCount(out2) + 1);
                    OsArrElem(out2)[OsArrCount(out2) - 1] = r;
                }
                return out2;
            }

            if (OsNameEq(name, nlen, "FILTER"))
            {
                OslVal out2 = OsArrMake(count);
                if (out2.K != VKArr) return OsNull();
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OslVal r = OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                    // A predicate must answer with a BOOLEAN; truthiness of a
                    // number is not accepted (matches the reference runtime).
                    if (r.K != VKBool || !r.B) continue;
                    OsArrSetCount(out2, OsArrCount(out2) + 1);
                    OsArrElem(out2)[OsArrCount(out2) - 1] = elems[i];
                }
                return out2;
            }

            if (OsNameEq(name, nlen, "FINDINDEX"))
            {
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OslVal r = OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                    if (r.K == VKBool && r.B) return OsNum(i);
                }
                return OsNum(-1);
            }

            if (OsNameEq(name, nlen, "ANY") || OsNameEq(name, nlen, "SOME"))
            {
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OslVal r = OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                    if (r.K == VKBool && r.B) return OsBool(true);
                }
                return OsBool(false);
            }

            if (OsNameEq(name, nlen, "ALL"))
            {
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OslVal r = OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                    if (r.K != VKBool || !r.B) return OsBool(false);
                }
                return OsBool(true);
            }

            if (OsNameEq(name, nlen, "FOREACH"))
            {
                for (int i = 0; i < count; i++)
                {
                    OslVal a1 = elems[i];
                    OsApplyCallable1(fn, a1);
                    if (_osErr) return OsNull();
                }
                return OsNull();
            }

            if (OsNameEq(name, nlen, "REDUCE"))
            {
                if (argc < 2) { OsFail("REDUCE() needs an initial value."); return OsNull(); }
                OslVal acc = OsEval(args[1]);
                if (_osErr) return OsNull();
                for (int i = 0; i < count; i++)
                {
                    acc = OsApplyCallable2(fn, acc, elems[i]);
                    if (_osErr) return OsNull();
                }
                return acc;
                return acc;
            }

            // FLATMAP: MAP, then one level of flattening.
            OslVal mapped = OsArrMake(count);
            if (mapped.K != VKArr) return OsNull();
            for (int i = 0; i < count; i++)
            {
                OslVal a1 = elems[i];
                OslVal r = OsApplyCallable1(fn, a1);
                if (_osErr) return OsNull();
                OsArrSetCount(mapped, OsArrCount(mapped) + 1);
                OsArrElem(mapped)[OsArrCount(mapped) - 1] = r;
            }
            return OsFlatten(mapped);
        }

        if (OsNameEq(name, nlen, "FLAT"))
        {
            return OsFlatten(recv);
        }

        if (OsNameEq(name, nlen, "INSERTAT"))
        {
            int at = (int)OsMArgNum(args, argc, 0, 0);
            OslVal item = OsMArg(args, argc, 1);
            if (_osErr) return OsNull();
            int c = OsArrCount(recv);
            if (at < 0) at = 0;
            if (at > c) at = c;
            OslVal grown = OsArrMake(c + 1);
            if (grown.K != VKArr) return OsNull();
            OslVal* ge = OsArrElem(grown);
            OslVal* oe = OsArrElem(recv);
            for (int i = 0; i < at; i++) ge[i] = oe[i];
            ge[at] = item;
            for (int i = at; i < c; i++) ge[i + 1] = oe[i];
            OsArrSetCount(grown, c + 1);
            return grown;
        }

        if (OsNameEq(name, nlen, "REMOVEAT"))
        {
            int at = (int)OsMArgNum(args, argc, 0, 0);
            int c = OsArrCount(recv);
            if (at < 0 || at >= c) return OsNull();
            OslVal* oe = OsArrElem(recv);
            for (int i = at; i < c - 1; i++) oe[i] = oe[i + 1];
            OsArrSetCount(recv, c - 1);
            return recv;
        }

        OsFailName(name, nlen, "Unknown ARRAY method '");
        return OsNull();
    }

    // One level of flattening: array elements are spliced in, everything else
    // is kept as it is (spec 0.51 FLAT).
    private static OslVal OsFlatten(OslVal arr)
    {
        int count = OsArrCount(arr);
        OslVal* elems = OsArrElem(arr);

        int total = 0;
        for (int i = 0; i < count; i++)
        {
            OslVal e = elems[i];
            total += e.K == VKArr ? OsArrCount(e) : 1;
        }

        OslVal out2 = OsArrMake(total);
        if (out2.K != VKArr) return OsNull();
        for (int i = 0; i < count; i++)
        {
            OslVal e = elems[i];
            if (e.K != VKArr) { OsArrSetCount(out2, OsArrCount(out2) + 1); OsArrElem(out2)[OsArrCount(out2) - 1] = e; continue; }
            int sub = OsArrCount(e);
            OslVal* subElems = OsArrElem(e);
            for (int k = 0; k < sub; k++)
            {
                OsArrSetCount(out2, OsArrCount(out2) + 1);
                OsArrElem(out2)[OsArrCount(out2) - 1] = subElems[k];
            }
        }
        return out2;
    }

    // Element order for SORT: numbers by value, strings by code unit, mixed by kind.
    private static bool OsSortLess(OslVal a, OslVal b)
    {
        if (a.K == VKNum && b.K == VKNum) return a.D < b.D;
        if (a.K == VKStr && b.K == VKStr)
        {
            int n = a.L < b.L ? a.L : b.L;
            char* x = (char*)a.P; char* y = (char*)b.P;
            for (int i = 0; i < n; i++)
            {
                if (x[i] != y[i]) return x[i] < y[i];
            }
            return a.L < b.L;
        }
        return a.K < b.K;
    }

    // Entry point: dispatch a method call whose receiver is a primitive value.
    // Returns false when the receiver is not one (the caller then tries other paths).
    private static bool OsValueMethod(OslVal recv, char* name, int nlen, Expr** args, int argc, out OslVal result)
    {
        result = OsNull();
        switch (recv.K)
        {
            case VKStr: result = OsStrMethod(recv, name, nlen, args, argc); return true;
            case VKNum: result = OsNumMethod(recv, name, nlen, args, argc); return true;
            case VKBool: result = OsBoolMethod(recv, name, nlen); return true;
            case VKArr: result = OsArrMethod(recv, name, nlen, args, argc); return true;
            default: return false;
        }
    }
}