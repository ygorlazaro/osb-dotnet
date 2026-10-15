// Text drawing: one syscall per string (the kernel blits the glyphs), so the
// program never pays a syscall per pixel.

namespace Osb.Xwin;

internal static unsafe class Font
{
    public const int GlyphWidth = 8;
    public const int GlyphHeight = 8;

    public static int DrawString(int x, int y, char* s, int len, byte fg, byte bg)
    {
        if (len > 0 && s != null)
            Sys.Call(0x48, Sys.Pack(x, y), (long)s, len, fg | (bg << 8));
        return x + len * GlyphWidth;
    }
}
