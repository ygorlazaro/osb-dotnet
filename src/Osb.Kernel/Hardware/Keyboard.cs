using System;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Hardware;

internal static unsafe class Keyboard
{
    // Scancode ring buffer filled by the IRQ1 handler, drained by the shell.
    private unsafe struct KbdRing
    {
        public fixed byte data[128];
        public int head;
        public int tail;
    }

    private static KbdRing _kbd;
    private static bool _kbdCtrl; // PS/2 Ctrl is currently held down

    public static void Push(byte sc)
    {
        int next = _kbd.tail + 1;
        if (next >= 128) next = 0;
        if (next != _kbd.head)
        {
            _kbd.data[_kbd.tail] = sc;
            _kbd.tail = next;
        }
    }

    public static int Pop()
    {
        if (_kbd.head == _kbd.tail) return -1;
        byte sc = _kbd.data[_kbd.head];
        int next = _kbd.head + 1;
        if (next >= 128) next = 0;
        _kbd.head = next;
        return sc;
    }

    public static void HandleScancode(byte sc)
    {
        if (sc == 0x1D) _kbdCtrl = true;             // Ctrl pressed
        else if (sc == 0x9D) _kbdCtrl = false;       // Ctrl released
        else if (sc == 0x2E && _kbdCtrl) ConsoleControl.RequestCtrlC();  // Ctrl+C (consume it)
        else Push(sc);
    }

    public static char MapScancode(byte sc, bool shift)
    {
        return sc switch
        {
            0x02 => shift ? '!' : '1',
            0x03 => shift ? '@' : '2',
            0x04 => shift ? '#' : '3',
            0x05 => shift ? '$' : '4',
            0x06 => shift ? '%' : '5',
            0x07 => shift ? '^' : '6',
            0x08 => shift ? '&' : '7',
            0x09 => shift ? '*' : '8',
            0x0A => shift ? '(' : '9',
            0x0B => shift ? ')' : '0',
            0x0C => shift ? '_' : '-',
            0x0D => shift ? '+' : '=',
            0x10 => 'Q',
            0x11 => 'W',
            0x12 => 'E',
            0x13 => 'R',
            0x14 => 'T',
            0x15 => 'Y',
            0x16 => 'U',
            0x17 => 'I',
            0x18 => 'O',
            0x19 => 'P',
            0x1A => shift ? '{' : '[',
            0x1B => shift ? '}' : ']',
            0x1E => 'A',
            0x1F => 'S',
            0x20 => 'D',
            0x21 => 'F',
            0x22 => 'G',
            0x23 => 'H',
            0x24 => 'J',
            0x25 => 'K',
            0x26 => 'L',
            0x27 => shift ? ':' : ';',
            0x28 => shift ? '"' : '\'',
            0x29 => shift ? '~' : '`',
            0x2B => shift ? '|' : '\\',
            0x2C => 'Z',
            0x2D => 'X',
            0x2E => 'C',
            0x2F => 'V',
            0x30 => 'B',
            0x31 => 'N',
            0x32 => 'M',
            0x33 => shift ? '<' : ',',
            0x34 => shift ? '>' : '.',
            0x35 => shift ? '?' : '/',
            0x39 => ' ',
            _ => (char)0
        };
    }

    public static bool IsCtrlPressed => _kbdCtrl;
}