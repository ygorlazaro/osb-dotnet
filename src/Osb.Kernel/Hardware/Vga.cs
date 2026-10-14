using System;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Hardware;

internal static unsafe class Vga
{
    private static int _row;
    private static int _col;
    private static byte _attr = 0x07;
    private static byte _serialAttr = 0x07;

    // ---- pipe capture ----
    // While capture is active, PutChar appends to a static buffer
    // instead of the console, so one pipeline stage can feed the
    // next. Bounded: overflow is dropped.

    private const int CapBufSize = 4096;
    private static bool _capture;
    private static int _capLen;
    private unsafe struct CapBuf { public fixed byte data[CapBufSize]; }
    private static CapBuf _cap;

    public static int CaptureLength => _capLen;

    // Captured text; valid from BeginCapture until the next one.
    // Returned as byte*: each char was stored as one byte.
    public static byte* CaptureBuffer
    {
        get { fixed (byte* p = _cap.data) return p; }
    }

    public static void BeginCapture()
    {
        _capture = true;
        _capLen = 0;
    }

    // Stops capturing; the buffer stays valid until the next BeginCapture.
    public static void EndCapture()
    {
        _capture = false;
    }
    public static byte Attr => _attr;
    public static int Width => Layout.VgaWidth;
    public static int Height => Layout.VgaHeight;
    public static void SetColor(byte fg, byte bg)
    {
        if (fg == bg) return;
        _attr = (byte)(((bg & 0x0F) << 4) | (fg & 0x0F));
    }
    public static void SetAttr(byte a)
    {
        _attr = a;
    }


    public static void Clear()
    {
        if (_capture) return;   // a piped CLS affects nothing but the next stage
        ushort* vga = (ushort*)Layout.VgaBase;
        for (int i = 0; i < Layout.VgaWidth * Layout.VgaHeight; i++)
            vga[i] = (ushort)(' ' | (_attr << 8));
        _row = 0;
        _col = 0;
        // Mirror the clear to the serial console: ESC[2J clears the screen,
        // ESC[H homes the cursor. Without this, CLS is a no-op on a headless
        // serial session (the VGA memory is wiped but the terminal keeps the
        // old text).
        Serial.PutChar((char)27);
        Serial.PutChar('[');
        Serial.PutChar('2');
        Serial.PutChar('J');
        Serial.PutChar((char)27);
        Serial.PutChar('[');
        Serial.PutChar('H');
    }

    public static void Scroll()
    {
        ushort* vga = (ushort*)Layout.VgaBase;
        for (int i = 0; i < (Layout.VgaHeight - 1) * Layout.VgaWidth; i++)
            vga[i] = vga[i + Layout.VgaWidth];
        for (int i = (Layout.VgaHeight - 1) * Layout.VgaWidth; i < Layout.VgaHeight * Layout.VgaWidth; i++)
            vga[i] = (ushort)(' ' | (_attr << 8));
        _row = Layout.VgaHeight - 1;
    }

    private static void WriteSerialDec(int v)
    {
        if (v >= 10) WriteSerialDec(v / 10);
        Serial.PutChar((char)('0' + (v % 10)));
    }

    // VGA palette -> ANSI color codes.
    // VGA: 0=black 1=blue 2=green 3=cyan 4=red 5=magenta 6=brown 7=light grey
    //      8=dark grey 9=light blue 10=light green 11=light cyan 12=light red
    //      13=light magenta 14=yellow 15=white
    // ANSI: 30=black 31=red 32=green 33=yellow 34=blue 35=magenta 36=cyan 37=white
    //       90=bright black 91=bright red 92=bright green 93=bright yellow
    //       94=bright blue 95=bright magenta 96=bright cyan 97=bright white
    private static int VgaToAnsiFg(int c)
    {
        switch (c)
        {
            case 0: return 30; case 1: return 34; case 2: return 32; case 3: return 36;
            case 4: return 31; case 5: return 35; case 6: return 33; case 7: return 37;
            case 8: return 90; case 9: return 94; case 10: return 92; case 11: return 96;
            case 12: return 91; case 13: return 95; case 14: return 93; default: return 97;
        }
    }

    private static int VgaToAnsiBg(int c)
    {
        switch (c)
        {
            case 0: return 40; case 1: return 44; case 2: return 42; case 3: return 46;
            case 4: return 41; case 5: return 45; case 6: return 43; case 7: return 47;
            case 8: return 100; case 9: return 104; case 10: return 102; case 11: return 106;
            case 12: return 101; case 13: return 105; case 14: return 103; default: return 107;
        }
    }

    private static void EmitSerialColor()
    {
        int fg = _attr & 0x0F;
        int bg = (_attr >> 4) & 0x0F;
        int fgCode = VgaToAnsiFg(fg);
        int bgCode = VgaToAnsiBg(bg);
        Serial.PutChar((char)27);
        Serial.PutChar('[');
        Serial.PutChar('0');
        Serial.PutChar(';');
        WriteSerialDec(fgCode);
        Serial.PutChar(';');
        WriteSerialDec(bgCode);
        Serial.PutChar('m');
        _serialAttr = _attr;
    }

    public static void PutChar(char c)
    {
        if (_capture)
        {
            if (_capLen < CapBufSize)
            {
                fixed (byte* p = _cap.data) p[_capLen++] = (byte)c;
            }
            return;
        }
        if (_attr != _serialAttr)
        {
            EmitSerialColor();
        }
        Serial.PutChar(c);
        if (c == '\n')
        {
            _col = 0;
            _row++;
        }
        else if (c == '\r')
        {
            _col = 0;
        }
        else
        {
            ushort* vga = (ushort*)Layout.VgaBase;
            vga[_row * Layout.VgaWidth + _col] = (ushort)(c | (_attr << 8));
            _col++;
            if (_col >= Layout.VgaWidth)
            {
                _col = 0;
                _row++;
            }
        }
        if (_row >= Layout.VgaHeight)
            Scroll();
    }

    public static void Write(string s)
    {
        if (s == null) return;
        fixed (char* p = s)
        {
            for (int i = 0; i < s.Length; i++)
                PutChar(p[i]);
        }
    }

    // Writes a char buffer of a known length (no managed strings in the
    // shell's completion/history paths).
    public static void WriteStr(char* s, int len)
    {
        for (int i = 0; i < len; i++)
            PutChar(s[i]);
    }

    public static void WriteLine(string s)
    {
        Write(s);
        PutChar('\n');
    }

    public static void WriteHex(ulong v)
    {
        Write("0x");
        bool started = false;
        for (int i = 15; i >= 0; i--)
        {
            int nib = (int)((v >> (i * 4)) & 0xF);
            if (nib != 0) started = true;
            if (!started && i != 0) continue;
            PutChar((char)(nib < 10 ? '0' + nib : 'A' + nib - 10));
        }
    }

    public static void WriteDec(ulong v)
    {
        char* buf = stackalloc char[20];
        int n = 0;
        if (v == 0) { PutChar('0'); return; }
        while (v != 0)
        {
            buf[n++] = (char)('0' + (int)(v % 10));
            v /= 10;
        }
        while (n > 0) PutChar(buf[--n]);
    }

    public static void WriteTwo(int v)
    {
        if (v < 10) PutChar('0');
        WriteDec((ulong)v);
    }


    public static void SetCursor(int row, int col)
    {
        if (row < 1) row = 1;
        if (row > Layout.VgaHeight) row = Layout.VgaHeight;
        if (col < 1) col = 1;
        if (col > Layout.VgaWidth) col = Layout.VgaWidth;
        _row = row - 1;
        _col = col - 1;
        ushort pos = (ushort)(_row * Layout.VgaWidth + _col);
        NativeMethods.outb(0x3D4, 0x0E);
        NativeMethods.outb(0x3D5, (byte)((pos >> 8) & 0xFF));
        NativeMethods.outb(0x3D4, 0x0F);
        NativeMethods.outb(0x3D5, (byte)(pos & 0xFF));
    }

    public static void WriteAt(int row, int col, string s)
    {
        SetCursor(row, col);
        Write(s);
    }

    public static void WriteAt(int row, int col, char* buf, int len)
    {
        SetCursor(row, col);
        WriteStr(buf, len);
    }

    public static ulong ParseUInt(char* s, int start, int end)
    {
        ulong v = 0;
        for (int i = start; i < end; i++)
        {
            if (s[i] < '0' || s[i] > '9') break;
            v = v * 10 + (ulong)(s[i] - '0');
        }
        return v;
    }
}