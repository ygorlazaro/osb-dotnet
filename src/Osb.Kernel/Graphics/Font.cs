// 8x8 character generator and text drawing.
//
// The glyphs are embedded (Graphics/Ascii8x8.cs) rather than copied out of
// the VGA BIOS: the firmware does not leave a usable font at the address the
// 8x16 table is documented to live at, and a kernel should not depend on what
// the firmware happens to leave lying around.
//
// The embedded font stores the leftmost pixel in the LOW bit, which is the
// opposite of the BIOS character generator, so the blitter below flips.

using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe class Font
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 8;
    public const int GlyphBytes = GlyphWidth;

    // The screen is a whole number of glyphs, which the desktop layout relies
    // on: 640/8 = 80 columns, 480/8 = 60 rows.
    public const int Columns = Layout.GfxWidth / GlyphWidth;
    public const int Rows = Layout.GfxHeight / GlyphHeight;

    public static bool Loaded => true;

    public static byte* Glyph(int c)
    {
        // Anything outside printable ASCII clamps to the blank glyph rather
        // than walking off the table.
        int i = c & 0xFF;
        if (i >= Ascii8x8.Count) i = 0;
        return Ascii8x8.Data + i * GlyphBytes;
    }

    // Draws one glyph with its top-left corner at (x, y).
    public static void DrawChar(int x, int y, char ch, byte fg, byte bg)
    {
        byte* g = Glyph((int)ch);
        for (int r = 0; r < GlyphHeight; r++)
        {
            int py = y + r;
            if (py < 0 || py >= Gfx.Height) continue;
            byte bits = g[r];
            for (int c = 0; c < GlyphWidth; c++)
            {
                int px = x + c;
                if (px < 0 || px >= Gfx.Width) continue;
                // Least significant bit is the leftmost pixel.
                bool on = (bits & (1 << c)) != 0;
                Gfx.SetPixel(px, py, on ? fg : bg);
            }
        }
    }

    // Draws a managed string and returns the x just past the last glyph.
    public static int DrawString(int x, int y, string s, byte fg, byte bg)
    {
        fixed (char* p = s)
        {
            for (int i = 0; i < s.Length; i++)
                DrawChar(x + i * GlyphWidth, y, p[i], fg, bg);
        }
        return x + s.Length * GlyphWidth;
    }

    public static int DrawString(int x, int y, char* s, int len, byte fg, byte bg)
    {
        for (int i = 0; i < len; i++)
            DrawChar(x + i * GlyphWidth, y, s[i], fg, bg);
        return x + len * GlyphWidth;
    }

    public static int StringWidth(int len) { return len * GlyphWidth; }
}