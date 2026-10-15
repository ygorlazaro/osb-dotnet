// The graphics front-end the desktop draws through.
//
// Two ways to put pixels on the screen, chosen once at Start():
//
//   * Bochs (preferred under the emulator): a linear 32bpp framebuffer at the
//     PCI card's BAR0, set through the DISPI VBE registers. No mode timing is
//     programmed by hand, so it scans out reliably.
//   * LegacyVga (real hardware): Mode 12h, four bit planes at A0000.
//
// Everything above this file (Desktop, WindowManager, Font, Cursor) talks only
// to this API and never to a port.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

// Which backend is drawing.
public enum Backend { None, Bochs, LegacyVga }

internal static unsafe class Gfx
{
    internal static Backend _backend = Backend.None;

    public static bool Active;

    // Set by Start() to the mode that actually came up. Defaults to the legacy
    // geometry so callers that touch these before Start() (Mouse.Init) are sane.
    public static int Width = Layout.GfxWidth;
    public static int Height = Layout.GfxHeight;

    // Bring up whichever display is present. Returns true if one of them
    // produced a usable drawing surface.
    public static bool Start(int width, int height)
    {
        // Stash the text character generator first: every graphics mode below
        // reuses the plane it lives in (see LegacyVga.SaveFont).
        LegacyVga.SaveFont();

        // Prefer the linear framebuffer: it needs no hand-programmed timing.
        if (Bochs.Init(width, height))
        {
            _backend = Backend.Bochs;
            Width = width;
            Height = height;
            Active = true;
            Report("Bochs VBE");
            return true;
        }

        // Fall back to legacy Mode 12h for machines without the Bochs device.
        if (LegacyVga.Init(width, height))
        {
            _backend = Backend.LegacyVga;
            Width = width;
            Height = height;
            Active = true;
            Report("Legacy VGA Mode 12h");
            return true;
        }

        _backend = Backend.None;
        Active = false;
        Vga.WriteLine("[Gfx] no usable display");
        return false;
    }

    // One status line, so a failed mode set is visible on the serial console
    // even when nothing appears on screen.
    private static void Report(string what)
    {
        Vga.Write("[Gfx] ");
        Vga.Write(what);
        Vga.Write(" ");
        Vga.WriteDec((ulong)Width);
        Vga.Write("x");
        Vga.WriteDec((ulong)Height);
        Vga.Write("\n");
    }

    // Idempotent: brings the mode up if it is not already, otherwise re-asserts
    // the state drawing depends on.
    public static void Enter()
    {
        if (!Active || _backend == Backend.None) Start(Width, Height);
        else Reassert();
    }

    // Re-assert the state that drawing depends on.
    public static void Reassert()
    {
        if (_backend == Backend.LegacyVga) LegacyVga.Reassert();
        Active = _backend != Backend.None;
    }

    // Back to 80x25 text, where the shell lives.
    public static void Exit()
    {
        if (_backend == Backend.Bochs) Bochs.Disable();
        // Always restore the text-mode register set, whichever backend was up,
        // so the shell's console comes back. The character generator has to be
        // restored as well, or the console would have no glyphs.
        LegacyVga.Exit();
        LegacyVga.RestoreFont();
        _backend = Backend.None;
        Active = false;
    }

    // ---- pixel access ----

    public static void SetPixel(int x, int y, byte c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        if (_backend == Backend.Bochs) Bochs.SetPixel(x, y, c);
        else if (_backend == Backend.LegacyVga) LegacyVga.SetPixel(x, y, c);
    }

    public static byte GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        if (_backend == Backend.Bochs) return Bochs.GetPixel(x, y);
        if (_backend == Backend.LegacyVga) return LegacyVga.GetPixel(x, y);
        return 0;
    }

    public static void Clear(byte c) { FillRect(0, 0, Width, Height, c); }

    // Fills with a solid colour.
    public static void FillRect(int x, int y, int w, int h, byte c)
    {
        if (w <= 0 || h <= 0) return;
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > Width) w = Width - x;
        if (y + h > Height) h = Height - y;
        if (w <= 0 || h <= 0) return;

        if (_backend == Backend.Bochs)
        {
            // The linear path is pixel addressed, so no byte alignment to fix.
            for (int r = 0; r < h; r++) Bochs.FillRun(x, y + r, w, c);
        }
        else if (_backend == Backend.LegacyVga)
        {
            LegacyVga.FillRect(x, y, w, h, c);
        }
    }

    public static void HLine(int x, int y, int w, byte c) { FillRect(x, y, w, 1, c); }

    public static void VLine(int x, int y, int h, byte c) { FillRect(x, y, 1, h, c); }

    public static void Rect(int x, int y, int w, int h, byte c)
    {
        if (w <= 0 || h <= 0) return;
        HLine(x, y, w, c);
        HLine(x, y + h - 1, w, c);
        VLine(x, y, h, c);
        VLine(x + w - 1, y, h, c);
    }

    // Classic raised/beveled frame: a light edge on top-left, dark on
    // bottom-right, which is what gives the Windows 3.11 windows their depth.
    public static void Bevel(int x, int y, int w, int h, byte face, byte light, byte dark)
    {
        if (w <= 0 || h <= 0) return;
        HLine(x + 1, y, w - 2, light);
        VLine(x, y + 1, h - 2, light);
        HLine(x + 1, y + h - 1, w - 2, dark);
        VLine(x + w - 1, y + 1, h - 2, dark);
        // Corners
        SetPixel(x, y, face);
        SetPixel(x + w - 1, y, face);
        SetPixel(x, y + h - 1, face);
        SetPixel(x + w - 1, y + h - 1, face);
    }
}
