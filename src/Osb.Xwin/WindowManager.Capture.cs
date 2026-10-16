// Window manager - drag and resize capture.

namespace Osb.Xwin;

internal static unsafe partial class WindowManager
{
    public static void BeginCapture(int index, int hitTest, int x, int y)
    {
        if (index < 0 || index >= _windowCount) return;

        _captureIndex = index;
        _captureHitTest = hitTest;
        _captureStartX = x;
        _captureStartY = y;

        Window* win = &_windows[index];
        _captureWindowX = win->x;
        _captureWindowY = win->y;
        _captureWindowW = win->w;
        _captureWindowH = win->h;

        BringToFront(index);
    }

    public static void UpdateCapture(int x, int y)
    {
        if (_captureIndex < 0 || _captureIndex >= _windowCount) return;

        Window* win = &_windows[_captureIndex];
        int dx = x - _captureStartX;
        int dy = y - _captureStartY;

        switch (_captureHitTest)
        {
            case HtCaption:
                win->x = _captureWindowX + dx;
                win->y = _captureWindowY + dy;
                if (win->x < WorkAreaLeft) win->x = WorkAreaLeft;
                if (win->y < 0) win->y = 0;
                if (win->x + win->w > Gfx.Width) win->x = Gfx.Width - win->w;
                if (win->y + win->h > Gfx.Height) win->y = Gfx.Height - win->h;
                UpdateClientArea(win);
                break;

            case HtLeft:
                win->x = _captureWindowX + dx;
                win->w = _captureWindowW - dx;
                if (win->w < 80) { win->w = 80; win->x = _captureWindowX + _captureWindowW - 80; }
                if (win->x < WorkAreaLeft) { win->w = _captureWindowW + _captureWindowX - WorkAreaLeft; win->x = WorkAreaLeft; }
                UpdateClientArea(win);
                break;

            case HtRight:
                win->w = _captureWindowW + dx;
                if (win->w < 80) win->w = 80;
                if (win->x + win->w > Gfx.Width) win->w = Gfx.Width - win->x;
                UpdateClientArea(win);
                break;

            case HtTop:
                win->y = _captureWindowY + dy;
                win->h = _captureWindowH - dy;
                if (win->h < 60) { win->h = 60; win->y = _captureWindowY + _captureWindowH - 60; }
                if (win->y < 0) { win->y = 0; win->h = _captureWindowH + _captureWindowY; }
                UpdateClientArea(win);
                break;

            case HtBottom:
                win->h = _captureWindowH + dy;
                if (win->h < 60) win->h = 60;
                if (win->y + win->h > Gfx.Height) win->h = Gfx.Height - win->y;
                UpdateClientArea(win);
                break;

            case HtTopLeft:
                win->x = _captureWindowX + dx;
                win->w = _captureWindowW - dx;
                win->y = _captureWindowY + dy;
                win->h = _captureWindowH - dy;
                if (win->w < 80) { win->w = 80; win->x = _captureWindowX + _captureWindowW - 80; }
                if (win->h < 60) { win->h = 60; win->y = _captureWindowY + _captureWindowH - 60; }
                if (win->x < WorkAreaLeft) { win->w = _captureWindowW + _captureWindowX - WorkAreaLeft; win->x = WorkAreaLeft; }
                if (win->y < 0) { win->y = 0; win->h = _captureWindowH + _captureWindowY; }
                UpdateClientArea(win);
                break;

            case HtTopRight:
                win->w = _captureWindowW + dx;
                win->y = _captureWindowY + dy;
                win->h = _captureWindowH - dy;
                if (win->w < 80) win->w = 80;
                if (win->h < 60) { win->h = 60; win->y = _captureWindowY + _captureWindowH - 60; }
                if (win->x + win->w > Gfx.Width) win->w = Gfx.Width - win->x;
                if (win->y < 0) { win->y = 0; win->h = _captureWindowH + _captureWindowY; }
                UpdateClientArea(win);
                break;

            case HtBottomLeft:
                win->x = _captureWindowX + dx;
                win->w = _captureWindowW - dx;
                win->h = _captureWindowH + dy;
                if (win->w < 80) { win->w = 80; win->x = _captureWindowX + _captureWindowW - 80; }
                if (win->h < 60) win->h = 60;
                if (win->x < WorkAreaLeft) { win->w = _captureWindowW + _captureWindowX - WorkAreaLeft; win->x = WorkAreaLeft; }
                if (win->y + win->h > Gfx.Height) win->h = Gfx.Height - win->y;
                UpdateClientArea(win);
                break;

            case HtBottomRight:
                win->w = _captureWindowW + dx;
                win->h = _captureWindowH + dy;
                if (win->w < 80) win->w = 80;
                if (win->h < 60) win->h = 60;
                if (win->x + win->w > Gfx.Width) win->w = Gfx.Width - win->x;
                if (win->y + win->h > Gfx.Height) win->h = Gfx.Height - win->y;
                UpdateClientArea(win);
                break;
        }

        _layoutVersion++;
    }

    public static void EndCapture() => _captureIndex = -1;

    private static void UpdateClientArea(Window* win)
    {
        win->cx = win->x + 2;
        win->cy = win->y + 18;
        win->cw = win->w - 4;
        win->ch = win->h - 20;
        if (win->cw < 0) win->cw = 0;
        if (win->ch < 0) win->ch = 0;
    }
}
