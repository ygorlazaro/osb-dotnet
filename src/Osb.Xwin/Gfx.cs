// The graphics front-end XWIN draws through. Same shape as the kernel's Gfx,
// but every call is an INT 21h round trip that the kernel's own engine
// performs (see Osb.Kernel/Cpu/SyscallGfx.cs).

namespace Osb.Xwin;

internal static unsafe class Gfx
{
    public static bool Active;
    public static int Width = 640;
    public static int Height = 480;

    public static bool Start()
    {
        if (Sys.Call(0x40, 0, 0, 0) != 0) { Active = false; return false; }
        int* info = stackalloc int[2];
        Sys.Call(0x42, (long)info, 0, 0);
        Width = info[0];
        Height = info[1];
        Active = true;
        return true;
    }

    public static void Enter() { if (!Active) Start(); }

    public static void Exit()
    {
        Sys.Call(0x41, 0, 0, 0);
        Active = false;
    }

    public static void Clear(byte c) => FillRect(0, 0, Width, Height, c);

    public static void FillRect(int x, int y, int w, int h, byte c) =>
        Sys.Call(0x43, Sys.Pack(x, y), Sys.Pack(w, h), c);

    public static void Rect(int x, int y, int w, int h, byte c) =>
        Sys.Call(0x44, Sys.Pack(x, y), Sys.Pack(w, h), c);

    public static void Bevel(int x, int y, int w, int h, byte face, byte light, byte dark) =>
        Sys.Call(0x45, Sys.Pack(x, y), Sys.Pack(w, h), face | (light << 8) | (dark << 16));

    public static void SetPixel(int x, int y, byte c) =>
        Sys.Call(0x46, Sys.Pack(x, y), c, 0);

    public static void HLine(int x, int y, int w, byte c) => FillRect(x, y, w, 1, c);

    public static void VLine(int x, int y, int h, byte c) => FillRect(x, y, 1, h, c);
}
