// Window manager - frame painting.
//
// Split out of WindowManager.cs (400-line rule). Draws borders, title bars and
// the caption buttons in Z-order; the caller paints each window's client area.

namespace Osb.Kernel.Graphics;

internal static unsafe partial class WindowManager
{
    // Draw window frames (borders, title bars) in Z-order
    // Client area painting is done by caller
    public static void PaintFrames()
    {
        if (!Gfx.Active) return;

        // Draw from bottom to top (lowest zOrder first)
        for (int z = 0; z < _windowCount; z++)
        {
            for (int i = 0; i < _windowCount; i++)
            {
                if (_windows[i].zOrder == z)
                {
                    DrawFrame(&_windows[i]);
                    break;
                }
            }
        }
    }

    private static void DrawFrame(Window* win)
    {
        if ((win->flags & WfVisible) == 0) return;

        bool active = (win->flags & WfActive) != 0;
        byte titleColor = active ? Palette.TitleActive : Palette.TitleInactive;
        byte titleTextColor = Palette.TitleText;
        byte frameColor = Palette.WindowFace;

        // Window frame (border)
        Gfx.Rect(win->x, win->y, win->w, win->h, Palette.DarkGray);

        // Title bar background
        Gfx.FillRect(win->x + 1, win->y + 1, win->w - 2, 17, titleColor);

        // Title text
        if (win->title != null && win->titleLen > 0)
        {
            Font.DrawString(win->x + 20, win->y + 4, win->title, win->titleLen, titleTextColor, titleColor);
        }

        // System menu icon (top-left)
        if ((win->flags & WfSystemMenu) != 0)
        {
            Gfx.FillRect(win->x + 2, win->y + 2, 14, 14, Palette.WindowFace);
            Gfx.Bevel(win->x + 2, win->y + 2, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.SetPixel(win->x + 6, win->y + 6, Palette.Black);
            Gfx.SetPixel(win->x + 10, win->y + 6, Palette.Black);
            Gfx.SetPixel(win->x + 6, win->y + 10, Palette.Black);
            Gfx.SetPixel(win->x + 10, win->y + 10, Palette.Black);
        }

        // Close button
        if ((win->flags & WfHasClose) != 0)
        {
            int bx = win->x + win->w - 18;
            int by = win->y + 2;
            Gfx.FillRect(bx, by, 14, 14, active ? Palette.LightRed : Palette.WindowFace);
            Gfx.Bevel(bx, by, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.HLine(bx + 3, by + 4, 8, Palette.Red);
            Gfx.HLine(bx + 3, by + 9, 8, Palette.Red);
            Gfx.VLine(bx + 4, by + 3, 10, Palette.Red);
            Gfx.VLine(bx + 9, by + 3, 10, Palette.Red);
        }

        // Maximize button
        if ((win->flags & WfHasMax) != 0)
        {
            int bx = win->x + win->w - 34;
            int by = win->y + 2;
            Gfx.FillRect(bx, by, 14, 14, Palette.WindowFace);
            Gfx.Bevel(bx, by, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.Rect(bx + 3, by + 4, 8, 8, Palette.Black);
        }

        // Minimize button
        if ((win->flags & WfHasMin) != 0)
        {
            int bx = win->x + win->w - 50;
            int by = win->y + 2;
            Gfx.FillRect(bx, by, 14, 14, Palette.WindowFace);
            Gfx.Bevel(bx, by, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.HLine(bx + 3, by + 11, 8, Palette.Black);
        }

        // Client area background
        Gfx.FillRect(win->cx, win->cy, win->cw, win->ch, Palette.WindowFace);

        // Window bevel (3D effect)
        Gfx.Bevel(win->x, win->y, win->w, win->h, frameColor, Palette.White, Palette.DarkGray);
    }
}
