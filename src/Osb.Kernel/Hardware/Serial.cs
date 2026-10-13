using System;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Hardware;

internal static unsafe class Serial
{
    private const ushort Com1Base = 0x3F8;

    public static void Init()
    {
        NativeMethods.outb(Com1Base + 1, 0x00);
        NativeMethods.outb(Com1Base + 3, 0x80);
        NativeMethods.outb(Com1Base + 0, 0x01);   // divisor low
        NativeMethods.outb(Com1Base + 1, 0x00);
        NativeMethods.outb(Com1Base + 3, 0x03);   // 8N1
        NativeMethods.outb(Com1Base + 2, 0xC7);   // FIFO: enable, clear, 14-byte trigger
        NativeMethods.outb(Com1Base + 4, 0x0B);   // DTR|RTS|OUT2
        NativeMethods.outb(Com1Base + 1, 0x01);   // IER: enable "received data available" (IRQ4)
    }

    public static void PutChar(char c)
    {
        if (c == '\n')
        {
            while ((NativeMethods.inb(Com1Base + 5) & 0x20) == 0) { }
            NativeMethods.outb(Com1Base, (byte)'\r');
        }
        while ((NativeMethods.inb(Com1Base + 5) & 0x20) == 0) { }
        NativeMethods.outb(Com1Base, (byte)c);
    }

    // Serial console ring (ASCII bytes) filled by the IRQ4 handler and drained by
    // the shell, so a headless session (-serial stdio) can drive the shell too.
    private unsafe struct CharRing
    {
        public fixed byte data[256];
        public int head;
        public int tail;
    }

    private static CharRing _ser;

    public static void Push(byte c)
    {
        int next = _ser.tail + 1;
        if (next >= 256) next = 0;
        if (next != _ser.head)
        {
            _ser.data[_ser.tail] = c;
            _ser.tail = next;
        }
    }

    public static int Pop()
    {
        if (_ser.head == _ser.tail) return -1;
        byte c = _ser.data[_ser.head];
        int next = _ser.head + 1;
        if (next >= 256) next = 0;
        _ser.head = next;
        return c;
    }
}