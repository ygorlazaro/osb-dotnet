// Input + timing wrappers. Values are pulled once per frame (Refresh) so the
// hot drawing path never pays a syscall for the cursor position.

namespace Osb.Xwin;

internal static unsafe class Mouse
{
    public static int X;
    public static int Y;
    public static bool LeftButton;
    public static bool RightButton;
    public static bool MiddleButton;
    public static bool Visible;

    public static void Refresh()
    {
        int* m = stackalloc int[4];
        Sys.Call(0x50, (long)m, 0, 0);
        X = m[0];
        Y = m[1];
        LeftButton = (m[2] & 1) != 0;
        RightButton = (m[2] & 2) != 0;
        MiddleButton = (m[2] & 4) != 0;
        Visible = m[3] != 0;
    }
}

internal static class Keyboard
{
    public static bool IsCtrlPressed;

    public static int Pop() => (int)Sys.Call(0x52, 0, 0, 0);
    public static bool ConsumeCtrlF4() => Sys.Call(0x53, 0, 0, 0) != 0;
    public static void Refresh() => IsCtrlPressed = Sys.Call(0x57, 0, 0, 0) != 0;
}

internal static class Cursor
{
    public static void Draw(int x, int y) => Sys.Call(0x51, Sys.Pack(x, y), 1, 0);
    public static void Restore() => Sys.Call(0x51, 0, 0, 0);
}

internal static class Pit
{
    public static ulong Ticks => (ulong)Sys.Call(0x54, 0, 0, 0);
    public static void Sleep(int ticks) => Sys.Call(0x55, ticks, 0, 0);
}

internal static unsafe class Rtc
{
    public static int Hour;
    public static int Minute;
    public static int Second;

    public static void Refresh()
    {
        byte* t = stackalloc byte[3];
        Sys.Call(0x56, (long)t, 0, 0);
        Hour = t[0];
        Minute = t[1];
        Second = t[2];
    }
}
