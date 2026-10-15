// PS/2 mouse driver (IRQ12).
//
// The mouse is initialized in stream mode with 3-byte packets (X/Y deltas + buttons).
// We expose a simple ring buffer of relative moves for the desktop shell to consume.

using System;
using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Hardware;

internal static unsafe class Mouse
{
    private const byte PortData = 0x60;
    private const byte PortStatus = 0x64;
    private const byte PortCommand = 0x64;

    // Mouse ring buffer
    private unsafe struct MouseRing
    {
        public fixed sbyte dx[256];
        public fixed sbyte dy[256];
        public fixed byte buttons[256];
        public int head;
        public int tail;
    }

    private static MouseRing _ring;
    private static byte _packetIndex;
    private static int _dx, _dy;           // decoded deltas (mouse space: +Y = up)
    private static bool _signX, _signY;    // sign bits captured from packet byte 0
    private static byte _buttons;

    public static int X { get; private set; } = 320;  // Start at center
    public static int Y { get; private set; } = 240;
    public static bool Visible { get; set; } = true;

    public static void Init()
    {
        // Wait for controller ready
        WaitWrite();
        NativeMethods.outb(PortCommand, 0xA8);  // Enable aux port (mouse)
        WaitWrite();
        NativeMethods.outb(PortCommand, 0x20);  // Get config byte
        byte config = 0;
        WaitRead();
        config = NativeMethods.inb(PortData);
        config |= 0x02;  // Enable IRQ12
        WaitWrite();
        NativeMethods.outb(PortCommand, 0x60);  // Set config byte
        WaitWrite();
        NativeMethods.outb(PortData, config);

        // Reset mouse
        WriteMouse(0xFF);  // Reset
        ReadMouse();       // ACK
        ReadMouse();       // BAT result (0xAA)
        ReadMouse();       // Device ID (0x00)

        // Set defaults: stream mode, 3-byte packets, enable reporting
        WriteMouse(0xF6);  // Set defaults
        ReadMouse();       // ACK
        WriteMouse(0xF4);  // Enable data reporting
        ReadMouse();       // ACK

        // Position at center
        X = Gfx.Width / 2;
        Y = Gfx.Height / 2;
    }

    private static void WaitWrite()
    {
        for (int i = 0; i < 100000; i++)
            if ((NativeMethods.inb(PortStatus) & 0x02) == 0) return;
    }

    private static void WaitRead()
    {
        for (int i = 0; i < 100000; i++)
            if ((NativeMethods.inb(PortStatus) & 0x01) != 0) return;
    }

    private static void WriteMouse(byte cmd)
    {
        WaitWrite();
        NativeMethods.outb(PortCommand, 0xD4);  // Write to aux port
        WaitWrite();
        NativeMethods.outb(PortData, cmd);
    }

    private static byte ReadMouse()
    {
        WaitRead();
        return NativeMethods.inb(PortData);
    }

    // Called from IRQ12 handler
    public static void HandleInterrupt()
    {
        byte status = NativeMethods.inb(PortStatus);
        if ((status & 0x20) == 0) return;  // Not from mouse

        byte data = NativeMethods.inb(PortData);

        switch (_packetIndex)
        {
            case 0:
                // First byte: bit3 is always 1; use it to resync after a
                // dropped/extra byte. Bits 0-2 buttons, bit4/bit5 X/Y sign,
                // bit6/bit7 overflow.
                if ((data & 0x08) == 0) break;  // out of sync, discard
                _buttons = (byte)(data & 0x07);
                _signX = (data & 0x10) != 0;
                _signY = (data & 0x20) != 0;
                _packetIndex = 1;
                break;
            case 1:
                // Second byte: low 8 bits of the 9-bit X delta. The 9th bit
                // (sign) lives in byte 0, so extend it here rather than
                // trusting an 8-bit two's-complement cast.
                _dx = data - (_signX ? 256 : 0);
                _packetIndex = 2;
                break;
            case 2:
                // Third byte: low 8 bits of the 9-bit Y delta (+Y = up).
                _dy = data - (_signY ? 256 : 0);

                // Clamp and update position
                int newX = X + _dx;
                int newY = Y - _dy;  // screen Y grows down; mouse +Y is up

                if (newX < 0) newX = 0;
                if (newX >= Gfx.Width) newX = Gfx.Width - 1;
                if (newY < 0) newY = 0;
                if (newY >= Gfx.Height) newY = Gfx.Height - 1;

                X = newX;
                Y = newY;

                // Push to ring buffer
                int next = _ring.tail + 1;
                if (next >= 256) next = 0;
                if (next != _ring.head)
                {
                    _ring.dx[_ring.tail] = (sbyte)_dx;
                    _ring.dy[_ring.tail] = (sbyte)_dy;
                    _ring.buttons[_ring.tail] = _buttons;
                    _ring.tail = next;
                }

                _packetIndex = 0;
                break;
        }
    }

    // Pop a mouse event from the ring
    public static bool Pop(out sbyte dx, out sbyte dy, out byte buttons)
    {
        if (_ring.head == _ring.tail)
        {
            dx = 0; dy = 0; buttons = 0;
            return false;
        }
        dx = _ring.dx[_ring.head];
        dy = _ring.dy[_ring.head];
        buttons = _ring.buttons[_ring.head];
        int next = _ring.head + 1;
        if (next >= 256) next = 0;
        _ring.head = next;
        return true;
    }

    public static bool LeftButton => (_buttons & 0x01) != 0;
    public static bool RightButton => (_buttons & 0x02) != 0;
    public static bool MiddleButton => (_buttons & 0x04) != 0;
}