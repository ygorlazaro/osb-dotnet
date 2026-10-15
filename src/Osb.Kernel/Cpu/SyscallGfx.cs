// INT 21h graphics + input services for ring-3 programs (XWIN).
//
// These are the functions the external XWIN.EXE drives the screen with. The
// syscall gate (Syscalls.Handle) hands the whole register frame here when AH
// is in the graphics/input range; the kernel still owns the hardware, so every
// call is drawn by the same Gfx/Font/Cursor engine the built-in desktop uses.
//
// Argument convention (as elsewhere in the gate): arg0 = RDI = frame[9],
// arg1 = RSI = frame[10], arg2 = RDX = frame[11], arg3 = RCX = frame[12];
// the result goes back in RAX = frame[14]. Coordinates are packed two per
// argument as ((lo16) << 16) | (hi16) and sign-extended here, so a single
// argument carries an (x, y) or (w, h) pair.
//
//   0x40 GFX_ENTER                    -> 0, or -1 if no display
//   0x41 GFX_EXIT                     -> 0
//   0x42 GFX_INFO(ptr)                -> width,height in an i32[2]
//   0x43 FILL(xy, wh, color)          -> 0
//   0x44 RECT(xy, wh, color)          -> 0
//   0x45 BEVEL(xy, wh, face|light<<8|dark<<16) -> 0
//   0x46 PIXEL(xy, color)             -> 0
//   0x47 LINE(xy0, xy1, color)        -> 0
//   0x48 TEXT(xy, ptr, len, color)    -> 0
//   0x50 MOUSE(ptr)                   -> writes i32[4]: x, y, buttons, visible
//   0x51 CURSOR(xy, show)             -> 0   (show 1 draws, 0 restores)
//   0x52 KEY                          -> scancode, or -1
//   0x53 CLOSE_REQUEST                -> 1 if Ctrl+F4 was pressed
//   0x54 TICKS                        -> PIT tick count
//   0x55 SLEEP(ticks)                 -> 0
//   0x56 TIME(ptr)                    -> writes u8[3]: h, m, s
//   0x57 MODS                         -> bit0 = Ctrl held

using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Cpu;

internal static unsafe class SyscallGfx
{
    private const ulong EFAULT = unchecked((ulong)-1);

    public static bool Handle(ulong func, ulong* frame)
    {
        ulong rdi = frame[9];
        ulong rsi = frame[10];
        ulong rdx = frame[11];
        ulong rcx = frame[12];

        switch (func)
        {
            case 0x40: // GFX_ENTER
                Gfx.Start(Gfx.Width, Gfx.Height);
                Gfx.Enter();
                if (Gfx.Active) Font.DrawString(0, 0, "OK", Palette.Black, Palette.Desktop);
                frame[14] = Gfx.Active ? 0UL : EFAULT;
                return true;

            case 0x41: // GFX_EXIT: back to text mode, console repainted
                Cursor.Restore();
                Gfx.Exit();
                Vga.Clear();
                frame[14] = 0;
                return true;

            case 0x42: // GFX_INFO(ptr): i32[2] = { width, height }
                if (!UserPtr(rdi, 8)) { frame[14] = EFAULT; return true; }
                ((int*)rdi)[0] = Gfx.Width;
                ((int*)rdi)[1] = Gfx.Height;
                frame[14] = 0;
                return true;

            case 0x43: // FILL(xy, wh, color)
                if (Gfx.Active) Gfx.FillRect(Hi(rdi), Lo(rdi), Hi(rsi), Lo(rsi), (byte)rdx);
                frame[14] = 0;
                return true;

            case 0x44: // RECT(xy, wh, color)
                if (Gfx.Active) Gfx.Rect(Hi(rdi), Lo(rdi), Hi(rsi), Lo(rsi), (byte)rdx);
                frame[14] = 0;
                return true;

            case 0x45: // BEVEL(xy, wh, face|light<<8|dark<<16)
                if (Gfx.Active)
                    Gfx.Bevel(Hi(rdi), Lo(rdi), Hi(rsi), Lo(rsi),
                              (byte)rdx, (byte)(rdx >> 8), (byte)(rdx >> 16));
                frame[14] = 0;
                return true;

            case 0x46: // PIXEL(xy, color)
                if (Gfx.Active) Gfx.SetPixel(Hi(rdi), Lo(rdi), (byte)rsi);
                frame[14] = 0;
                return true;

            case 0x47: // LINE(xy0, xy1, color)
                if (Gfx.Active) Line(Hi(rdi), Lo(rdi), Hi(rsi), Lo(rsi), (byte)rdx);
                frame[14] = 0;
                return true;

            case 0x48: // TEXT(xy, ptr, len, fg|bg<<8)
                if (!UserPtr(rsi, (int)(rdx > 1024 ? 1024 : rdx))) { frame[14] = EFAULT; return true; }
                if (Gfx.Active)
                {
                    int len = (int)(rdx > 1024 ? 1024 : rdx);
                    Font.DrawString(Hi(rdi), Lo(rdi), (char*)rsi, len, (byte)rcx, (byte)(rcx >> 8));
                }
                frame[14] = 0;
                return true;

            case 0x50: // MOUSE(ptr): i32[4] = { x, y, buttons, visible }
                if (!UserPtr(rdi, 16)) { frame[14] = EFAULT; return true; }
                int* m = (int*)rdi;
                m[0] = Mouse.X;
                m[1] = Mouse.Y;
                m[2] = (Mouse.LeftButton ? 1 : 0) | (Mouse.RightButton ? 2 : 0) | (Mouse.MiddleButton ? 4 : 0);
                m[3] = Mouse.Visible ? 1 : 0;
                frame[14] = 0;
                return true;

            case 0x51: // CURSOR(xy, show)
                if ((byte)rsi != 0) Cursor.Draw(Hi(rdi), Lo(rdi));
                else Cursor.Restore();
                frame[14] = 0;
                return true;

            case 0x52: // KEY
                frame[14] = unchecked((ulong)Keyboard.Pop());
                return true;

            case 0x53: // CLOSE_REQUEST
                frame[14] = Keyboard.ConsumeCtrlF4() ? 1UL : 0UL;
                return true;

            case 0x54: // TICKS
                frame[14] = Pit.Ticks;
                return true;

            case 0x55: // SLEEP(ticks)
            {
                ulong start = Pit.Ticks;
                ulong want = rdi;
                while (Pit.Ticks - start < want) NativeMethods.halt();
                frame[14] = 0;
                return true;
            }

            case 0x56: // TIME(ptr): u8[3] = { h, m, s }
                if (!UserPtr(rdi, 3)) { frame[14] = EFAULT; return true; }
                ((byte*)rdi)[0] = (byte)Rtc.Hour;
                ((byte*)rdi)[1] = (byte)Rtc.Minute;
                ((byte*)rdi)[2] = (byte)Rtc.Second;
                frame[14] = 0;
                return true;

            case 0x57: // MODS
                frame[14] = Keyboard.IsCtrlPressed ? 1UL : 0UL;
                return true;

            case 0x58: // POWEROFF -> ACPI S5 (QEMU exits); never returns
                NativeMethods.osb_poweroff();
                frame[14] = 0;
                return true;

            case 0x59: // REBOOT -> 8042 reset pulse; never returns
                NativeMethods.osb_reboot();
                frame[14] = 0;
                return true;

            default:
                return false;
        }
    }

    // The low half of a packed coordinate pair; sign-extended so negative
    // origins survive the round trip.
    private static int Lo(ulong v) => (short)(v & 0xFFFF);

    // The high half of a packed coordinate pair.
    private static int Hi(ulong v) => (short)((v >> 16) & 0xFFFF);

    private static bool UserPtr(ulong p, int n)
    {
        if (n < 0) return false;
        return p >= Layout.UserBase && p + (ulong)n <= Layout.UserLimit;
    }

    // Bresenham so XWIN can draw diagonal lines without per-pixel syscalls.
    private static void Line(int x0, int y0, int x1, int y1, byte c)
    {
        int dx = x1 - x0; if (dx < 0) dx = -dx;
        int dy = y1 - y0; if (dy < 0) dy = -dy;
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        for (;;)
        {
            Gfx.SetPixel(x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err << 1;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}
