// Shell history: a fixed-size ring of executed command lines.
//
// No managed arrays (the freestanding AOT runtime has none): the ring lives in
// a fixed-size struct, the way Environment does. Entry numbers are the
// monotonic push order (1-based), so `HISTORY` prints stable numbers, and the
// user can re-run entry N with the "(:N)" syntax.
//
// The ring also drives Up/Down navigation: `nav` is the cursor into the
// visible entries (-1 = editing the current line, whose draft is kept so Down
// can restore it). Buffer access is index-based inside `fixed` blocks, the way
// Serial.cs and Environment.cs do it.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Shell;

internal static unsafe class History
{
    public const int MaxEntries = Layout.MaxHistory;      // ring slots
    public const int LineCap = Layout.HistoryLineCap;     // chars per entry

    private unsafe struct Ring
    {
        public fixed char buf[Layout.MaxHistory * Layout.HistoryLineCap];
        public fixed int lens[Layout.MaxHistory];
        public fixed char draft[Layout.HistoryLineCap];
        public int pushed;     // total entries ever pushed (entry numbers)
        public int nav;        // cursor: visible entry index, or -1 = idle
        public int draftLen;
    }

    private static Ring _ring;

    // Entries currently visible in the ring.
    public static int Count => _ring.pushed < MaxEntries ? _ring.pushed : MaxEntries;

    // Records a submitted line. Empty lines and consecutive duplicates are
    // skipped; navigation resets to the draft.
    public static void Push(char* line, int len)
    {
        if (len <= 0) return;
        if (len > LineCap - 1) len = LineCap - 1;

        if (Count > 0)
        {
            int last = Count - 1;
            if (_ring.lens[SlotOf(last)] == len)
            {
                bool same = true;
                for (int k = 0; k < len; k++)
                    if (EntryChar(last, k) != line[k]) { same = false; break; }
                if (same) { _ring.nav = -1; return; }
            }
        }

        int slot = _ring.pushed % MaxEntries;
        fixed (char* b = _ring.buf)
        {
            for (int k = 0; k < len; k++) b[slot * LineCap + k] = line[k];
        }
        _ring.lens[slot] = len;
        _ring.pushed++;
        _ring.nav = -1;
    }

    // Copies entry number `num` (1-based push order) into `dst`.
    public static bool TryRecall(int num, char* dst, out int len)
    {
        len = 0;
        if (num <= 0 || Count == 0) return false;
        int oldest = _ring.pushed - Count + 1;
        if (num < oldest || num > _ring.pushed) return false;
        int idx = num - oldest;
        int l = _ring.lens[SlotOf(idx)];
        if (l > LineCap - 1) l = LineCap - 1;
        int slot = SlotOf(idx);
        fixed (char* b = _ring.buf)
        {
            for (int k = 0; k < l; k++) dst[k] = b[slot * LineCap + k];
        }
        len = l;
        return true;
    }

    // True when the line looks like a "(:N)" recall expression (well-formed or
    // not), so the shell can tell a broken recall from an ordinary command.
    public static bool LooksLikeRecall(char* line, int len)
    {
        return len >= 4 && line[0] == '(' && line[1] == ':' && line[len - 1] == ')';
    }

    // Recognizes "(:N)" and copies entry N into outLine. Returns false when the
    // syntax is malformed or the number is out of range.
    public static bool ParseRecall(char* line, int len, char* outLine, out int outLen)
    {
        outLen = 0;
        if (len < 4 || line[0] != '(' || line[1] != ':') return false;
        int n = 0;
        int i = 2;
        bool digits = false;
        while (i < len && line[i] >= '0' && line[i] <= '9')
        {
            n = n * 10 + (line[i] - '0');
            i++;
            digits = true;
        }
        if (!digits || i != len - 1 || line[i] != ')') return false;
        return TryRecall(n, outLine, out outLen);
    }

    // HISTORY [n]: numbered listing of the n latest entries (default 100).
    public static void ListCommand(char* line, int len)
    {
        int want = 100;
        int v = ShellText.ParseIntAt(line, 7, len);
        if (v > 0) want = v;
        int count = Count;
        if (want > count) want = count;
        if (want == 0) { Vga.WriteLine("History is empty."); return; }

        int start = count - want;                 // first visible index shown
        int oldest = _ring.pushed - count + 1;
        for (int i = 0; i < want; i++)
        {
            int idx = start + i;
            ShellText.WriteDecPad((ulong)(oldest + idx), 5);
            Vga.Write("  ");
            int l = _ring.lens[SlotOf(idx)];
            int slot = SlotOf(idx);
            fixed (char* b = _ring.buf)
            {
                for (int k = 0; k < l; k++) Vga.PutChar(b[slot * LineCap + k]);
            }
            Vga.PutChar('\n');
        }
    }

    // Drops the navigation cursor (after running a line or Ctrl+C).
    public static void NavReset() => _ring.nav = -1;

    // One older entry into the edit buffer (Up). Returns false when there is
    // nothing to recall.
    public static bool Prev(char* line, ref int len)
    {
        int count = Count;
        if (count == 0) return false;
        if (_ring.nav < 0)
        {
            // remember the line being edited so Down can restore it
            int n = len > LineCap - 1 ? LineCap - 1 : len;
            fixed (char* d = _ring.draft)
            {
                for (int k = 0; k < n; k++) d[k] = line[k];
            }
            _ring.draftLen = n;
            _ring.nav = count - 1;
        }
        else if (_ring.nav > 0)
        {
            _ring.nav--;
        }
        CopyVisible(_ring.nav, line, ref len);
        return true;
    }

    // One newer entry (Down); past the newest entry the saved draft returns.
    public static bool Next(char* line, ref int len)
    {
        if (_ring.nav < 0) return false;
        int count = Count;
        if (_ring.nav < count - 1)
        {
            _ring.nav++;
            CopyVisible(_ring.nav, line, ref len);
        }
        else
        {
            _ring.nav = -1;
            int n = _ring.draftLen;
            fixed (char* d = _ring.draft)
            {
                for (int k = 0; k < n; k++) line[k] = d[k];
            }
            len = n;
        }
        return true;
    }

    // ---- ring index helpers ----

    // Physical slot of visible entry `idx` (0 = oldest visible). The ring is
    // indexed from the oldest live slot, so idx 0 maps to physical
    // (pushed - Count) % MaxEntries.
    private static int SlotOf(int idx)
    {
        int oldestSlot = (_ring.pushed - Count) % MaxEntries;
        return (oldestSlot + idx) % MaxEntries;
    }

    private static char EntryChar(int idx, int pos)
    {
        int slot = SlotOf(idx);
        return _ring.buf[slot * LineCap + pos];
    }

    private static void CopyVisible(int idx, char* line, ref int len)
    {
        int slot = SlotOf(idx);
        int l = _ring.lens[slot];
        if (l > LineCap - 1) l = LineCap - 1;
        fixed (char* b = _ring.buf)
        {
            for (int k = 0; k < l; k++) line[k] = b[slot * LineCap + k];
        }
        len = l;
    }
}
