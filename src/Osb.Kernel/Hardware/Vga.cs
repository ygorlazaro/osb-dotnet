using System;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Hardware;

internal static unsafe class Vga
{
    private static int _row;
    private static int _col;

    public static void Clear()
    {
        ushort* vga = (ushort*)Layout.VgaBase;
        for (int i = 0; i < Layout.VgaWidth * Layout.VgaHeight; i++)
            vga[i] = (ushort)(' ' | (Layout.Attr << 8));
        _row = 0;
        _col = 0;
    }

    public static void Scroll()
    {
        ushort* vga = (ushort*)Layout.VgaBase;
        for (int i = 0; i < (Layout.VgaHeight - 1) * Layout.VgaWidth; i++)
            vga[i] = vga[i + Layout.VgaWidth];
        for (int i = (Layout.VgaHeight - 1) * Layout.VgaWidth; i < Layout.VgaHeight * Layout.VgaWidth; i++)
            vga[i] = (ushort)(' ' | (Layout.Attr << 8));
        _row = Layout.VgaHeight - 1;
    }

    public static void PutChar(char c)
    {
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
            vga[_row * Layout.VgaWidth + _col] = (ushort)(c | (Layout.Attr << 8));
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