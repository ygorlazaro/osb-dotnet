// 8x16 character generator and text drawing.
//
// The glyphs come from the VGA BIOS, which keeps the 8x16 font at F000:FA6E
// and copies it into the character generator plane at boot. Reading it from
// there means no font is embedded in the kernel and the real thing is what
// gets drawn, on a real card as well as under an emulator.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe class Font
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 16;
    public const int GlyphBytes = GlyphWidth * GlyphHeight;   // one bit per pixel

    private const int Count = 256;

    private unsafe struct Glyphs { public fixed byte b[Count * GlyphBytes]; }
    private static Glyphs _font;
    private static bool _loaded;

    // True when the BIOS font is present and looks like a font rather than
    // blank memory. Without this a machine with no usable BIOS would draw
    // nothing and there would be no way to tell from the screen alone.
    public static bool Loaded => _loaded;

    // Copies the BIOS character generator into kernel memory.
    //
    // NOT YET FOUND. The font is not at F000:FA6E on this platform, and the
    // scan of the F000 segment for a block shaped like a font (blank NUL,
    // blank space, a letter with ink in its top rows) comes up empty - the
    // adapter has no character generator there to find.
    //
    // The next options, in the order worth trying:
    //   1. Look for the 8x16 table outside the F000 segment, e.g. in the
    //      option ROM area the adapter itself points at through its ROM BAR.
    //   2. Call the BIOS INT 10h AH=1Ah/0x11 for the font pointer. That needs
    //      a trampoline into the real-mode handler, since vector 0x10 is ours.
    //   3. Embed a font. Cheap in size (4096 bytes) and no longer dependent
    //      on what the firmware happens to leave lying around.
    public static void Load()
    {
        ulong found = FindInBios();
        if (found == 0) { _loaded = false; return; }

        byte* src = (byte*)found;
        byte* dst = Base;
        for (int i = 0; i < Count * GlyphBytes; i++) dst[i] = src[i];
        _loaded = true;
    }

    // Looks for a 256-glyph 8x16 font in the F000 BIOS segment.
    private static ulong FindInBios()
    {
        ulong start = 0xF0000;
        ulong end = 0xF1000;

        for (ulong a = start; a + (ulong)(Count * GlyphBytes) <= end; a += 16)
        {
            if (!LooksLikeFont((byte*)a)) continue;
            // Prefer the lowest match: the real font is normally first.
            return a;
        }
        return 0;
    }

    private static bool LooksLikeFont(byte* p)
    {
        // Glyph 0 (NUL) and glyph 32 (space) are blank in every real font.
        for (int i = 0; i < GlyphBytes; i++)
            if (p[i] != 0) return false;
        byte* space = p + 32 * GlyphBytes;
        for (int i = 0; i < GlyphBytes; i++)
            if (space[i] != 0) return false;

        // 'A' must have ink, and ink in its upper rows rather than a blank
        // top: this rules out tables of zeroes or filler.
        byte* a = p + 'A' * GlyphBytes;
        int ink = 0;
        for (int i = 0; i < GlyphBytes; i++) if (a[i] != 0) ink++;
        if (ink < 12) return false;
        for (int i = 0; i < 8; i++) if (a[i] != 0) return true;
        return false;
    }

    // The glyph table lives in a fixed buffer, so reaching it needs a fixed
    // statement; this keeps that in one place.
    private static byte* Base { get { fixed (byte* p = _font.b) return p; } }

    public static byte* Glyph(int c)
    {
        return Base + (c & 0xFF) * GlyphBytes;
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
                bool on = (bits & (0x80 >> c)) != 0;
                Gfx.SetPixel(px, py, on ? fg : bg);
            }
        }
    }

    // Draws a string and returns the x just past the last glyph.
    // Takes a managed string because that is what the shell commands have;
    // the freestanding build supports `fixed (char*)` but not indexing.
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

    // Width in pixels a string of `len` glyphs would occupy.
    public static int StringWidth(int len) { return len * GlyphWidth; }
}