// Window manager for overlapping windows (Windows 3.11 style).
//
// Manages a list of windows with Z-order, hit-testing, dragging, sizing.
// Painting is handled by the caller (Desktop). The drag/resize capture
// logic lives in WindowManager.Capture.cs and frame painting in
// WindowManager.Paint.cs.

using Osb.Kernel.Graphics;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe partial class WindowManager
{
    // Maximum number of windows
    private const int MaxWindows = 16;

    // Window flags
    public const int WfVisible = 0x01;
    public const int WfActive = 0x02;
    public const int WfMaximized = 0x04;
    public const int WfMinimized = 0x08;
    public const int WfHasClose = 0x10;
    public const int WfHasMax = 0x20;
    public const int WfHasMin = 0x40;
    public const int WfSystemMenu = 0x80;

    // Window kinds for painting
    public const int WkProgman = 1;
    public const int WkApplication = 2;
    public const int WkGroup = 3;
    public const int WkCustom = 4;

    // Hit-test regions
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

    private static int _windowCount;
    private static Window* _windows;
    private static int _activeIndex = -1;
    private static int _captureIndex = -1;
    private static int _captureHitTest;
    private static int _captureStartX, _captureStartY;
    private static int _captureWindowX, _captureWindowY;
    private static int _captureWindowW, _captureWindowH;

    // Window structure
    private struct Window
    {
        public int x, y, w, h;      // Window bounds
        public int cx, cy, cw, ch;  // Client area bounds
        public int flags;
        public int kind;            // Window kind for painting
        public int zOrder;          // 0 = bottom, higher = top
        public char* title;         // Window title
        public int titleLen;
        public void* userData;      // For application-specific data
    }

    public static int WindowCount => _windowCount;

    public static void Init()
    {
        _windowCount = 0;
        _windows = (Window*)Kernel.OsAlloc(sizeof(Window) * MaxWindows);
        _activeIndex = -1;
        _captureIndex = -1;
    }

    // Create a new window
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
        win->zOrder = index;  // New windows on top
        win->title = title;
        win->titleLen = titleLen;
        win->userData = null;

        // Calculate client area (inside frame)
        int frameX = 2;
        int frameY = 18;  // Title bar height
        win->cx = x + frameX;
        win->cy = y + frameY;
        win->cw = w - frameX * 2;
        win->ch = h - frameY - frameX;

        if (win->cw < 0) win->cw = 0;
        if (win->ch < 0) win->ch = 0;

        BringToFront(index);
        return index;
    }

    // Destroy a window
    public static void Destroy(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        // Remove from list
        for (int i = index; i < _windowCount - 1; i++)
        {
            _windows[i] = _windows[i + 1];
        }
        _windowCount--;

        // Adjust active index
        if (_activeIndex == index)
            _activeIndex = -1;
        else if (_activeIndex > index)
            _activeIndex--;

        // Adjust capture index
        if (_captureIndex == index)
            _captureIndex = -1;
        else if (_captureIndex > index)
            _captureIndex--;
    }

    // Bring window to front (top of Z-order)
    public static void BringToFront(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        Window* win = &_windows[index];
        int oldZ = win->zOrder;

        // Shift windows between oldZ and top down by one
        for (int i = 0; i < _windowCount; i++)
        {
            if (_windows[i].zOrder > oldZ)
                _windows[i].zOrder--;
        }
        win->zOrder = _windowCount - 1;

        // Update active window
        if (_activeIndex >= 0 && _activeIndex != index)
        {
            _windows[_activeIndex].flags &= ~WfActive;
        }

        _activeIndex = index;
        win->flags |= WfActive;

        Invalidate();
    }

    // Send window to back
    public static void SendToBack(int index)
    {
        if (index < 0 || index >= _windowCount) return;

        Window* win = &_windows[index];
        int oldZ = win->zOrder;

        for (int i = 0; i < _windowCount; i++)
        {
            if (_windows[i].zOrder < oldZ)
                _windows[i].zOrder++;
        }
        win->zOrder = 0;

        Invalidate();
    }

    // Hit test a window
    public static int HitTest(int index, int x, int y)
    {
        if (index < 0 || index >= _windowCount) return HtNowhere;

        Window* win = &_windows[index];
        if ((win->flags & WfVisible) == 0) return HtNowhere;

        // Check if point is in window
        if (x < win->x || x >= win->x + win->w || y < win->y || y >= win->y + win->h)
            return HtNowhere;

        // Title bar (top 18 pixels)
        if (y < win->y + 18)
        {
            // System menu (top-left 16x16)
            if (x < win->x + 16 && (win->flags & WfSystemMenu) != 0)
                return HtSysMenu;

            // Close button (top-right 16x16)
            if (x >= win->x + win->w - 16 && (win->flags & WfHasClose) != 0)
                return HtClose;

            // Maximize button
            if (x >= win->x + win->w - 32 && x < win->x + win->w - 16 && (win->flags & WfHasMax) != 0)
                return HtMaximize;

            // Minimize button
            if (x >= win->x + win->w - 48 && x < win->x + win->w - 32 && (win->flags & WfHasMin) != 0)
                return HtMinimize;

            return HtCaption;
        }

        // Client area
        if (x >= win->cx && x < win->cx + win->cw && y >= win->cy && y < win->cy + win->ch)
            return HtClient;

        // Resize borders (2 pixels)
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

    // Invalidate - mark for repaint
    public static void Invalidate() { }

    // Get active window index
    public static int GetActiveIndex() => _activeIndex;

    // Get window at screen coordinates (topmost)
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

    // Get window kind
    public static int GetKind(int index)
    {
        if (index < 0 || index >= _windowCount) return 0;
        return _windows[index].kind;
    }

    // Get client area
    public static void GetClientArea(int index, out int cx, out int cy, out int cw, out int ch)
    {
        cx = cy = cw = ch = 0;
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        cx = win->cx; cy = win->cy; cw = win->cw; ch = win->ch;
    }

    // Get window title
    public static void GetTitle(int index, out char* title, out int titleLen)
    {
        title = null; titleLen = 0;
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        title = win->title;
        titleLen = win->titleLen;
    }

    // Check if capturing
    public static bool IsCapturing => _captureIndex >= 0;

    // Get window bounds
    public static void GetBounds(int index, out int x, out int y, out int w, out int h)
    {
        x = y = w = h = 0;
        if (index < 0 || index >= _windowCount) return;
        Window* win = &_windows[index];
        x = win->x; y = win->y; w = win->w; h = win->h;
    }
}
