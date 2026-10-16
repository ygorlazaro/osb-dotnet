// Window manager for overlapping windows (Windows 3.11 style).
//
// Ported from the kernel's built-in Graphics/WindowManager.cs so the external
// XWIN desktop behaves identically; the only change is that backing memory
// comes from the local arena instead of the kernel heap.

namespace Osb.Xwin;

internal static unsafe partial class WindowManager
{
    private const int MaxWindows = 16;

    public const int WfVisible = 0x01;
    public const int WfActive = 0x02;
    public const int WfMaximized = 0x04;
    public const int WfMinimized = 0x08;
    public const int WfHasClose = 0x10;
    public const int WfHasMax = 0x20;
    public const int WfHasMin = 0x40;
    public const int WfSystemMenu = 0x80;

    public const int WkProgman = 1;
    public const int WkApplication = 2;
    public const int WkGroup = 3;
    public const int WkCustom = 4;

    public const int HtNowhere = 0;
    public const int HtCaption = 1;
    public const int HtClose = 2;
    public const int HtMaximize = 3;
    public const int HtMinimize = 4;
    public const int HtLeft = 5;
    public const int HtRight = 6;
    public const int HtTop = 7;
    public const int HtBottom = 8;
    public const int HtTopLeft = 9;
    public const int HtTopRight = 10;
    public const int HtBottomLeft = 11;
    public const int HtBottomRight = 12;
    public const int HtClient = 13;
    public const int HtSysMenu = 14;

    // Left edge of the usable desktop. The deskbar is drawn over everything,
    // so maximize and drag/resize keep windows from sliding underneath it.
    public static int WorkAreaLeft;

    private static int _windowCount;
    private static Window* _windows;
    private static int _activeIndex = -1;
    private static int _captureIndex = -1;

    private static int _layoutVersion;
    public static int LayoutVersion => _layoutVersion;
    private static int _captureHitTest;
    private static int _captureStartX, _captureStartY;
    private static int _captureWindowX, _captureWindowY;
    private static int _captureWindowW, _captureWindowH;

    private struct Window
    {
        public int x, y, w, h;
        public int cx, cy, cw, ch;
        public int flags;
        public int kind;
        public int zOrder;
        public char* title;
        public int titleLen;
        public void* userData;
        public int savedX, savedY, savedW, savedH;
    }

    public static int WindowCount => _windowCount;

    public static void Init()
    {
        _windowCount = 0;
        _windows = (Window*)Arena.Alloc(sizeof(Window) * MaxWindows);
        _activeIndex = -1;
        _captureIndex = -1;
        _layoutVersion = 0;
    }

    public static int Create(int x, int y, int w, int h, char* title, int titleLen, int flags, int kind)
    {
        if (_windowCount >= MaxWindows) return -1;

        int index = _windowCount++;
        Window* win = &_windows[index];

        win->x = x;
        win->y = y;
        win->w = w;
        win->h = h;
        win->flags = flags | WfVisible;
        win->kind = kind;
        win->zOrder = index;
        win->title = title;
        win->titleLen = titleLen;
        win->userData = null;

        int frameX = 2;
        int frameY = 18;
        win->cx = x + frameX;
        win->cy = y + frameY;
        win->cw = w - frameX * 2;
        win->ch = h - frameY - frameX;

        if (win->cw < 0) win->cw = 0;
        if (win->ch < 0) win->ch = 0;

        BringToFront(index);
        return index;
    }

    public static void Destroy(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        for (int i = index; i < _windowCount - 1; i++)
            _windows[i] = _windows[i + 1];
        _windowCount--;

        _layoutVersion++;

        bool wasActive = _activeIndex == index;
        if (wasActive)
            _activeIndex = -1;
        else if (_activeIndex > index)
            _activeIndex--;

        if (_captureIndex == index)
            _captureIndex = -1;
        else if (_captureIndex > index)
            _captureIndex--;

        // The destroyed window leaves a hole in the z values; close it, or
        // every loop over z in [0, count) skips the window left on top.
        NormalizeZ();
        if (wasActive) ActivateTopmost();
    }

    public static void BringToFront(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        Window* win = &_windows[index];
        win->flags &= ~WfMinimized;     // raising a window also restores it
        win->zOrder = 0x7FFFFFFF;       // above everyone; NormalizeZ ranks it last
        NormalizeZ();

        if (_activeIndex >= 0 && _activeIndex != index)
            _windows[_activeIndex].flags &= ~WfActive;

        _activeIndex = index;
        win->flags |= WfActive;
        _layoutVersion++;
    }

    public static void SendToBack(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        _windows[index].zOrder = -1;    // below everyone; NormalizeZ ranks it first
        NormalizeZ();
        _layoutVersion++;
    }

    // Index of the window whose zOrder is z (0 = bottom), or -1.
    public static int IndexAtZ(int z)
    {
        for (int i = 0; i < _windowCount; i++)
            if (_windows[i].zOrder == z) return i;
        return -1;
    }

    // Rewrites zOrder as a permutation of 0..count-1 (bottom..top), keeping the
    // current relative order and breaking ties by array index. Painting
    // (App.Render, PaintFrames) and WindowAt all walk z over [0, count), so
    // they rely on this invariant after every create/destroy/raise/minimize.
    private static void NormalizeZ()
    {
        int* rank = stackalloc int[MaxWindows];
        for (int i = 0; i < _windowCount; i++)
        {
            int zi = _windows[i].zOrder;
            int r = 0;
            for (int j = 0; j < _windowCount; j++)
            {
                int zj = _windows[j].zOrder;
                if (zj < zi || (zj == zi && j < i)) r++;
            }
            rank[i] = r;
        }
        for (int i = 0; i < _windowCount; i++)
            _windows[i].zOrder = rank[i];
    }

    // Gives focus to the topmost visible, non-minimized window (or nobody).
    private static void ActivateTopmost()
    {
        _activeIndex = -1;
        for (int i = 0; i < _windowCount; i++)
            _windows[i].flags &= ~WfActive;

        for (int z = _windowCount - 1; z >= 0; z--)
        {
            int i = IndexAtZ(z);
            if (i < 0) continue;
            if ((_windows[i].flags & (WfVisible | WfMinimized)) != WfVisible) continue;
            _activeIndex = i;
            _windows[i].flags |= WfActive;
            return;
        }
    }

    public static int HitTest(int index, int x, int y)
    {
        if (index < 0 || index >= _windowCount) return HtNowhere;

        Window* win = &_windows[index];
        // A minimized window is not on screen, so it must not catch clicks.
        if ((win->flags & (WfVisible | WfMinimized)) != WfVisible) return HtNowhere;

        if (x < win->x || x >= win->x + win->w || y < win->y || y >= win->y + win->h)
            return HtNowhere;

        if (y < win->y + 18)
        {
            if (x < win->x + 16 && (win->flags & WfSystemMenu) != 0)
                return HtSysMenu;
            if (x >= win->x + win->w - 16 && (win->flags & WfHasClose) != 0)
                return HtClose;
            if (x >= win->x + win->w - 32 && x < win->x + win->w - 16 && (win->flags & WfHasMax) != 0)
                return HtMaximize;
            if (x >= win->x + win->w - 48 && x < win->x + win->w - 32 && (win->flags & WfHasMin) != 0)
                return HtMinimize;
            return HtCaption;
        }

        if (x >= win->cx && x < win->cx + win->cw && y >= win->cy && y < win->cy + win->ch)
            return HtClient;

        bool left = x < win->x + 2;
        bool right = x >= win->x + win->w - 2;
        bool top = y < win->y + 2;
        bool bottom = y >= win->y + win->h - 2;

        if (left && top) return HtTopLeft;
        if (right && top) return HtTopRight;
        if (left && bottom) return HtBottomLeft;
        if (right && bottom) return HtBottomRight;
        if (left) return HtLeft;
        if (right) return HtRight;
        if (top) return HtTop;
        if (bottom) return HtBottom;

        return HtNowhere;
    }

    public static int GetActiveIndex() => _activeIndex;

    public static void Minimize(int index)
    {
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        win->flags |= WfMinimized;
        win->flags &= ~WfActive;
        // Sink it to the bottom of the stack (NormalizeZ makes -1 the lowest
        // rank); BringToFront restores it. Focus moves to the next window.
        win->zOrder = -1;
        NormalizeZ();
        if (_activeIndex == index) ActivateTopmost();
        _layoutVersion++;
    }

    public static void Maximize(int index, int taskbarHeight)
    {
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        win->flags ^= WfMaximized; // toggle
        if ((win->flags & WfMaximized) != 0)
        {
            // Save current position and expand to full screen (minus taskbar).
            win->savedX = win->x;
            win->savedY = win->y;
            win->savedW = win->w;
            win->savedH = win->h;
            win->x = WorkAreaLeft;
            win->y = 0;
            win->w = Gfx.Width - WorkAreaLeft;
            win->h = Gfx.Height - taskbarHeight;
        }
        else
        {
            // Restore saved position.
            win->x = win->savedX;
            win->y = win->savedY;
            win->w = win->savedW;
            win->h = win->savedH;
        }
        UpdateClientArea(win);
        _layoutVersion++;
    }

    public static int WindowAt(int x, int y)
    {
        for (int z = _windowCount - 1; z >= 0; z--)
        {
            for (int i = 0; i < _windowCount; i++)
            {
                if (_windows[i].zOrder == z)
                {
                    if (HitTest(i, x, y) != HtNowhere)
                        return i;
                    break;
                }
            }
        }
        return -1;
    }

    public static int GetKind(int index)
    {
        if (index < 0 || index >= _windowCount) return 0;
        return _windows[index].kind;
    }

    public static void GetWindowInfo(int index, out char* title, out int titleLen,
                                     out bool visible, out bool minimized, out bool active)
    {
        title = null; titleLen = 0;
        visible = false; minimized = false; active = false;
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        title = win->title;
        titleLen = win->titleLen;
        visible = (win->flags & WfVisible) != 0;
        minimized = (win->flags & WfMinimized) != 0;
        active = (win->flags & WfActive) != 0;
    }

    public static void GetClientArea(int index, out int cx, out int cy, out int cw, out int ch)
    {
        cx = cy = cw = ch = 0;
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        cx = win->cx; cy = win->cy; cw = win->cw; ch = win->ch;
    }

    public static bool IsCapturing => _captureIndex >= 0;
}
