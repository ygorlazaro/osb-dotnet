// OSTK - OSB Toolkit. Core types.
//
// Freestanding GUI library for XWIN applications. Built on the INT 21h
// graphics/input syscalls used by Osb.Xwin. All state is arena-allocated;
// no GC, no exceptions, no heap allocation in the hot path.

namespace Osb.Toolkit;

public struct Rect
{
    public int X;
    public int Y;
    public int W;
    public int H;
}

public struct Point
{
    public int X;
    public int Y;
}

public enum HitTest : byte
{
    Nowhere = 0,
    Client = 1,
    Caption = 2,
    Close = 3,
    Maximize = 4,
    Minimize = 5,
    Left = 6,
    Right = 7,
    Top = 8,
    Bottom = 9,
    TopLeft = 10,
    TopRight = 11,
    BottomLeft = 12,
    BottomRight = 13,
}

public enum ControlKind : byte
{
    None = 0,
    Window = 1,
    Button = 2,
    Label = 3,
    Panel = 4,
}

[System.Flags]
public enum WindowStyle : byte
{
    None = 0,
    Visible = 0x01,
    Active = 0x02,
    HasClose = 0x10,
    HasMax = 0x20,
    HasMin = 0x40,
    SystemMenu = 0x80,
}

public static class Osk
{
    public static bool Contains(ref Rect r, int x, int y)
    {
        return x >= r.X && x < r.X + r.W && y >= r.Y && y < r.Y + r.H;
    }

    public static Rect Intersect(ref Rect a, ref Rect b)
    {
        Rect r;
        r.X = a.X > b.X ? a.X : b.X;
        r.Y = a.Y > b.Y ? a.Y : b.Y;
        int aRight = a.X + a.W;
        int bRight = b.X + b.W;
        int right = aRight < bRight ? aRight : bRight;
        int aBottom = a.Y + a.H;
        int bBottom = b.Y + b.H;
        int bottom = aBottom < bBottom ? aBottom : bBottom;
        r.W = right - r.X;
        r.H = bottom - r.Y;
        if (r.W < 0) r.W = 0;
        if (r.H < 0) r.H = 0;
        return r;
    }
}
