// OSTK - top-level windows. Thin OSTK-styled API over Osb.Xwin.WindowManager.
//
// An OSTK window handle is a Osb.Xwin.WindowManager index. OSTK tracks nothing
// per-window beyond what WindowManager already owns; controls are tagged with
// their owning window index in the control pool.

namespace Osb.Toolkit;

internal static unsafe class Window
{
    public const int MaxWindows = 16;
    public const int NoWindow = -1;

    private static int _count;
    public static int Count => _count;

    public static int Create(int x, int y, int w, int h, char* title, int titleLen, WindowStyle style)
    {
        int index = Osb.Xwin.WindowManager.Create(
            x, y, w, h, title, titleLen,
            (int)style, Osb.Xwin.WindowManager.WkApplication);
        if (index >= 0) _count++;
        return index;
    }

    public static void Destroy(int index)
    {
        if (index < 0 || index >= Osb.Xwin.WindowManager.WindowCount) return;
        Osb.Xwin.WindowManager.Destroy(index);
        if (_count > 0) _count--;
    }

    public static void BringToFront(int index) => Osb.Xwin.WindowManager.BringToFront(index);

    public static int GetActiveIndex() => Osb.Xwin.WindowManager.GetActiveIndex();

    public static HitTest HitTest(int index, int x, int y)
    {
        return (HitTest)Osb.Xwin.WindowManager.HitTest(index, x, y);
    }

    public static void GetClientArea(int index, out Rect rect)
    {
        rect = default;
        Osb.Xwin.WindowManager.GetClientArea(
            index, out int cx, out int cy, out int cw, out int ch);
        rect.X = cx; rect.Y = cy; rect.W = cw; rect.H = ch;
    }

    public static void GetWindowInfo(int index, out char* title, out int titleLen,
                                     out bool visible, out bool minimized, out bool active)
    {
        title = null; titleLen = 0;
        visible = false; minimized = false; active = false;
        Osb.Xwin.WindowManager.GetWindowInfo(index, out title, out titleLen,
            out visible, out minimized, out active);
    }

    public static void PaintFrames() => Osb.Xwin.WindowManager.PaintFrames();

    public static bool IsMinimized(int index)
    {
        GetWindowInfo(index, out _, out _, out _, out bool minimized, out _);
        return minimized;
    }
}
