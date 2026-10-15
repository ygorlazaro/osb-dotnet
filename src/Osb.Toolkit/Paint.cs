// OSTK - control rendering. Standard painters for buttons, labels and panels.

using Osb.Xwin;

namespace Osb.Toolkit;

internal static unsafe class Paint
{
    public static void Button(Control* c)
    {
        Rect r = Controls.ScreenBounds(c);
        bool active = c->UserData == 0;
        byte face = active ? Palette.HighlightBg : Palette.WindowFace;
        Graphics.FillRect(r.X, r.Y, r.W, r.H, face);
        Graphics.Bevel(r.X, r.Y, r.W, r.H, face, Palette.White, Palette.DarkGray);
        int tx = r.X + 6;
        int ty = r.Y + (r.H - Osb.Xwin.Font.GlyphHeight) / 2;
        if (ty < r.Y + 1) ty = r.Y + 1;
        Graphics.DrawString(tx, ty, c->Text, c->TextLen,
            active ? Palette.Highlight : Palette.Black, face);
    }

    public static void Label(Control* c)
    {
        Rect r = Controls.ScreenBounds(c);
        Graphics.DrawString(r.X, r.Y, c->Text, c->TextLen, Palette.IconText, Palette.Desktop);
    }

    public static void Panel(Control* c)
    {
        Rect r = Controls.ScreenBounds(c);
        Graphics.FillRect(r.X, r.Y, r.W, r.H, Palette.WindowFace);
        Graphics.Rect(r.X, r.Y, r.W, r.H, Palette.DarkGray);
    }
}
