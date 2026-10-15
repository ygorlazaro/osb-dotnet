// OSTK - controls. Arena-allocated pool of control structs for the whole
// desktop. Controls are tagged with the window they belong to (-1 = desktop /
// screen coordinates). No GC, no `new class`, no heap.

namespace Osb.Toolkit;

internal unsafe struct Control
{
    public ControlKind Kind;
    public Rect Bounds;
    public bool Visible;
    public bool Enabled;
    public char* Text;
    public int TextLen;
    public int OwnerWindow;   // OSTK window index, or -1 for desktop
    public int UserData;      // command id / window index to open
}

internal static unsafe class Controls
{
    private static Control* _items;
    private static int _count;
    private const int MaxControls = 128;

    public static void Init()
    {
        _items = (Control*)Osb.Xwin.Arena.Alloc(sizeof(Control) * MaxControls);
        _count = 0;
    }

    public static int Count => _count;

    public static int Add(ControlKind kind, Rect bounds, char* text, int textLen,
                          int ownerWindow, int userData)
    {
        if (_count >= MaxControls) return -1;
        int index = _count++;
        _items[index] = new Control
        {
            Kind = kind,
            Bounds = bounds,
            Visible = true,
            Enabled = true,
            Text = text,
            TextLen = textLen,
            OwnerWindow = ownerWindow,
            UserData = userData,
        };
        return index;
    }

    public static ref Control Get(int index) => ref _items[index];

    public static Control* Ptr(int index) => &_items[index];

    internal static Rect ScreenBounds(Control* c)
    {
        Rect r = c->Bounds;
        if (c->OwnerWindow >= 0)
        {
            Rect client;
            Window.GetClientArea(c->OwnerWindow, out client);
            r.X = client.X + c->Bounds.X;
            r.Y = client.Y + c->Bounds.Y;
        }
        return r;
    }

    public static void PaintAll()
    {
        for (int i = 0; i < _count; i++) PaintControl(&_items[i]);
    }

    public static void PaintOwner(int ownerWindow)
    {
        for (int i = 0; i < _count; i++)
        {
            Control* c = &_items[i];
            if (c->OwnerWindow != ownerWindow) continue;
            PaintControl(c);
        }
    }

    private static void PaintControl(Control* c)
    {
        if (!c->Visible) return;
        switch (c->Kind)
        {
            case ControlKind.Button: Paint.Button(c); break;
            case ControlKind.Label: Paint.Label(c); break;
            case ControlKind.Panel: Paint.Panel(c); break;
        }
    }

    public static int HitControl(int x, int y, int ownerWindow)
    {
        for (int i = 0; i < _count; i++)
        {
            Control* c = &_items[i];
            if (!c->Visible || !c->Enabled) continue;
            if (c->OwnerWindow != ownerWindow) continue;
            Rect r = ScreenBounds(c);
            if (Osk.Contains(ref r, x, y)) return i;
        }
        return -1;
    }
}
