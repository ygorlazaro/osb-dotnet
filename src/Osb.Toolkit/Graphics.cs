// OSTK - graphics primitives. Thin wrappers over the Osb.Xwin INT 21h
// Gfx/Font bindings so toolkit code never calls syscalls directly.

namespace Osb.Toolkit;

internal static unsafe class Graphics
{
    public static void Clear(byte c) => Osb.Xwin.Gfx.Clear(c);
    public static void FillRect(int x, int y, int w, int h, byte c) =>
        Osb.Xwin.Gfx.FillRect(x, y, w, h, c);
    public static void Rect(int x, int y, int w, int h, byte c) =>
        Osb.Xwin.Gfx.Rect(x, y, w, h, c);
    public static void Bevel(int x, int y, int w, int h, byte face, byte light, byte dark) =>
        Osb.Xwin.Gfx.Bevel(x, y, w, h, face, light, dark);
    public static void SetPixel(int x, int y, byte c) => Osb.Xwin.Gfx.SetPixel(x, y, c);
    public static void HLine(int x, int y, int w, byte c) => Osb.Xwin.Gfx.HLine(x, y, w, c);
    public static void VLine(int x, int y, int h, byte c) => Osb.Xwin.Gfx.VLine(x, y, h, c);
    public static int DrawString(int x, int y, char* s, int len, byte fg, byte bg) =>
        Osb.Xwin.Font.DrawString(x, y, s, len, fg, bg);
    public static int TextWidth(int len) => len * Osb.Xwin.Font.GlyphWidth;
}
