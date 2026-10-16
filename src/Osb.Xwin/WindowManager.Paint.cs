// Window manager - frame painting (borders, title bar, caption buttons).

namespace Osb.Xwin;

internal static unsafe partial class WindowManager
{
    public static void PaintFrames()
    {
        if (!Gfx.Active) return;

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

    // Paints one window's frame (border, title bar, buttons, client background).
    // App.Render calls this window by window, bottom to top, so each window is
    // fully drawn (frame + client + controls) before the next, higher one.
    public static void PaintFrame(int index)
    {
        if (!Gfx.Active) return;
        if (index < 0 || index >= _windowCount) return;
        DrawFrame(&_windows[index]);
    }

    private static void DrawFrame(Window* win)
    {
        if ((win->flags & WfVisible) == 0) return;
        if ((win->flags & WfMinimized) != 0) return;

        bool active = (win->flags & WfActive) != 0;
        byte titleColor = active ? Palette.TitleActive : Palette.TitleInactive;
        byte titleTextColor = Palette.TitleText;
        byte frameColor = Palette.WindowFace;

        // Make the whole window opaque first. The border is 1px at x, the title
        // bar starts at x+1 and the client at x+2, so without this the 1px ring
        // between them (x+1, x+w-2, y+h-2) is never painted and whatever lies
        // underneath shows through the frame.
        Gfx.FillRect(win->x, win->y, win->w, win->h, frameColor);

        Gfx.Rect(win->x, win->y, win->w, win->h, Palette.DarkGray);
        Gfx.FillRect(win->x + 1, win->y + 1, win->w - 2, 17, titleColor);

        if (win->title != null && win->titleLen > 0)
            Font.DrawString(win->x + 20, win->y + 4, win->title, win->titleLen, titleTextColor, titleColor);

        if ((win->flags & WfSystemMenu) != 0)
        {
            Gfx.FillRect(win->x + 2, win->y + 2, 14, 14, Palette.WindowFace);
            Gfx.Bevel(win->x + 2, win->y + 2, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.SetPixel(win->x + 6, win->y + 6, Palette.Black);
            Gfx.SetPixel(win->x + 10, win->y + 6, Palette.Black);
            Gfx.SetPixel(win->x + 6, win->y + 10, Palette.Black);
            Gfx.SetPixel(win->x + 10, win->y + 10, Palette.Black);
        }

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

        if ((win->flags & WfHasMax) != 0)
        {
            int bx = win->x + win->w - 34;
            int by = win->y + 2;
            Gfx.FillRect(bx, by, 14, 14, Palette.WindowFace);
            Gfx.Bevel(bx, by, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.Rect(bx + 3, by + 4, 8, 8, Palette.Black);
        }

        if ((win->flags & WfHasMin) != 0)
        {
            int bx = win->x + win->w - 50;
            int by = win->y + 2;
            Gfx.FillRect(bx, by, 14, 14, Palette.WindowFace);
            Gfx.Bevel(bx, by, 14, 14, Palette.WindowFace, Palette.White, Palette.DarkGray);
            Gfx.HLine(bx + 3, by + 11, 8, Palette.Black);
        }

        Gfx.FillRect(win->cx, win->cy, win->cw, win->ch, Palette.WindowFace);
        Gfx.Bevel(win->x, win->y, win->w, win->h, frameColor, Palette.White, Palette.DarkGray);
    }
}
