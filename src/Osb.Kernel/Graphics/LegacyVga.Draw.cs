// Legacy VGA: the planar drawing primitives.
//
// Kept apart from LegacyVga.cs so the mode/register programming and the pixel
// work stay readable on their own. Everything here writes through the A0000
// aperture, selecting planes with the sequencer map mask (writes) and the
// graphics-controller read-map-select (reads).

using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe partial class LegacyVga
{
    public static void SetPixel(int x, int y, byte c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int off = y * Layout.GfxBytesPerRow + (x >> 3);
        byte bit = (byte)(1 << (HighBit - (x & 7)));
        for (int n = 0; n < 4; n++)
        {
            SelectReadPlane(n);
            SetPlaneMask((byte)(1 << n));
            byte v = Base[off];
            if ((c & (1 << n)) != 0) v |= bit; else v &= (byte)~bit;
            Base[off] = v;
        }
        SetPlaneMask(0x0F);
    }

    public static byte GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        int off = y * Layout.GfxBytesPerRow + (x >> 3);
        byte mask = (byte)(1 << (HighBit - (x & 7)));
        byte c = 0;
        for (int n = 0; n < 4; n++)
        {
            SelectReadPlane(n);
            if ((Base[off] & mask) != 0) c |= (byte)(1 << n);
        }
        return c;
    }

    public static void Clear(byte c) { FillRect(0, 0, Width, Height, c); }

    // Fills with a solid colour.
    //
    // A colour index is one bit per plane, so filling is two passes: clear all
    // four planes in the rectangle, then set only the planes the colour needs.
    // Painting a single plane without clearing the others is the classic
    // planar bug - it renders whatever the other planes happened to hold.
    public static void FillRect(int x, int y, int w, int h, byte c)
    {
        if (w <= 0 || h <= 0) return;
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > Width) w = Width - x;
        if (y + h > Height) h = Height - y;
        if (w <= 0 || h <= 0) return;

        if ((x & 7) != 0 || (w & 7) != 0)
        {
            // Partial bytes at the edges: correctness over speed.
            for (int r = 0; r < h; r++)
                for (int i = 0; i < w; i++)
                    SetPixel(x + i, y + r, c);
            return;
        }

        int bytes = w >> 3;
        SetPlaneMask(0x0F);
        Span(0x00, x, y, h, bytes);
        for (int n = 0; n < 4; n++)
        {
            if ((c & (1 << n)) != 0)
            {
                SetPlaneMask((byte)(1 << n));
                Span(0xFF, x, y, h, bytes);
            }
        }
        SetPlaneMask(0x0F);
    }

    // Writes one value across a run of whole bytes in the selected plane(s).
    private static void Span(byte v, int x, int y, int h, int bytes)
    {
        byte* row0 = Base + y * Layout.GfxBytesPerRow + (x >> 3);
        for (int r = 0; r < h; r++)
        {
            byte* row = row0 + r * Layout.GfxBytesPerRow;
            for (int i = 0; i < bytes; i++) row[i] = v;
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
        SetPixel(x, y, face);
        SetPixel(x + w - 1, y, face);
        SetPixel(x, y + h - 1, face);
        SetPixel(x + w - 1, y + h - 1, face);
    }
}
