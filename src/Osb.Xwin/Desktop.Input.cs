// XWIN desktop - mouse and keyboard input handling.
//
// Split out of Desktop.cs (400-line rule); shares the desktop's static state
// through the partial class. Ported from the kernel's Shell/Desktop.Input.cs.

namespace Osb.Xwin;

internal static unsafe partial class Desktop
{
    private const int DoubleClickTicks = 30;   // ~300 ms at 100 Hz

    private static void ProcessMouse()
    {
        _lastMouseX = Mouse.X;
        _lastMouseY = Mouse.Y;

        bool leftNow = Mouse.LeftButton;
        bool rightNow = Mouse.RightButton;

        if (leftNow && !_mouseLeftDown)
        {
            HandleMouseDown(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = true;
        }
        else if (!leftNow && _mouseLeftDown)
        {
            HandleMouseUp(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = false;
        }

        if (rightNow && !_mouseRightDown)
        {
            HandleMouseDown(Mouse.X, Mouse.Y, 2);
            _mouseRightDown = true;
        }
        else if (!rightNow && _mouseRightDown)
        {
            HandleMouseUp(Mouse.X, Mouse.Y, 2);
            _mouseRightDown = false;
        }

        // Keep dragging/sizing while a button is held.
        if (WindowManager.IsCapturing && (leftNow || rightNow))
            WindowManager.UpdateCapture(Mouse.X, Mouse.Y);
    }

    private static void HandleMouseDown(int x, int y, int button)
    {
        if (button != 1) return;

        // Taskbar: the Start button raises the Program Manager.
        int ty = Gfx.Height - TaskbarHeight;
        if (y >= ty)
        {
            if (x >= _taskbarStartX && x < _taskbarStartX + _startButtonW)
            {
                if (_progmanWindow >= 0) WindowManager.BringToFront(_progmanWindow);
                _startMenuOpen = !_startMenuOpen;
            }
            return;
        }

        int hit = WindowManager.WindowAt(x, y);
        if (hit >= 0)
        {
            int ht = WindowManager.HitTest(hit, x, y);
            if (ht == WindowManager.HtClose) { CloseWindow(hit); return; }

       

            if (ht == WindowManager.HtCaption || ht == WindowManager.HtSysMenu)
            {
                WindowManager.BeginCapture(hit, WindowManager.HtCaption, x, y);
                return;
            }

            if (ht >= WindowManager.HtLeft && ht <= WindowManager.HtBottomRight)
            {
                WindowManager.BeginCapture(hit, ht, x, y);
                return;
            }

            if (ht == WindowManager.HtClient)
            {
                WindowManager.BringToFront(hit);
                if (IsDoubleClick(x, y, button) && WindowManager.GetKind(hit) == WindowManager.WkProgman)
                    OpenAppFromPoint(x, y);
                return;
            }
            return;
        }

        // Desktop background: select / open icons.
        int icon = IconAt(x, y);
        _selectedIcon = icon;
        if (icon >= 0 && IsDoubleClick(x, y, button))
            OpenIcon(icon);
    }

    private static void HandleMouseUp(int x, int y, int button)
    {
        WindowManager.EndCapture();
    }

    private static int IconAt(int x, int y)
    {
        for (int i = 0; i < _iconCount; i++)
        {
            Icon* ic = &_icons[i];
            if (x >= ic->x && x < ic->x + IconWidth &&
                y >= ic->y && y < ic->y + IconHeight + 16)
                return i;
        }
        return -1;
    }

    private static bool IsDoubleClick(int x, int y, int button)
    {
        int now = (int)Pit.Ticks;
        bool dbl = button == _lastClickButton &&
                   now - _lastClickTime < DoubleClickTicks &&
                   Abs(x - _lastClickX) < 4 &&
                   Abs(y - _lastClickY) < 4;

        _lastClickTime = now;
        _lastClickX = x;
        _lastClickY = y;
        _lastClickButton = button;
        return dbl;
    }

    private static void ProcessKeyboard()
    {
        if (_frameCount > 100 && Keyboard.ConsumeCtrlF4())
        {
            CloseActiveWindow();
            return;
        }

        int sc;
        while ((sc = Keyboard.Pop()) >= 0)
        {
            if (sc == 0x01)              // Esc: clear the icon selection
                _selectedIcon = -1;
        }
    }

    private static void OpenIcon(int i)
    {
        if (i < 0 || i >= _iconCount) return;
        Icon* ic = &_icons[i];

        if (ic->iconType == 1)
        {
            WindowManager.Create(120 + i * 18, 90 + i * 14, 360, 240,
                ic->name, ic->nameLen,
                WindowManager.WfVisible | WindowManager.WfHasClose | WindowManager.WfHasMax |
                WindowManager.WfHasMin | WindowManager.WfSystemMenu,
                WindowManager.WkGroup);
        }
        else
        {
            WindowManager.Create(150 + i * 10, 110 + i * 8, 320, 200,
                ic->name, ic->nameLen,
                WindowManager.WfVisible | WindowManager.WfHasClose | WindowManager.WfSystemMenu,
                WindowManager.WkApplication);
        }
    }

    // Double-clicking a program item in the Program Manager opens it.
    private static void OpenAppFromPoint(int x, int y)
    {
        int item = ProgmanItemAt(x, y);
        if (item < 0) return;

        char* name;
        int nameLen;
        switch (item)
        {
            case 0: name = _strWrite; nameLen = _strWriteLen; break;
            case 1: name = _strPaintbrush; nameLen = _strPaintbrushLen; break;
            case 2: name = _strCalculator; nameLen = _strCalculatorLen; break;
            case 3: name = _strNotepad; nameLen = _strNotepadLen; break;
            case 4: name = _strTerminal; nameLen = _strTerminalLen; break;
            default: name = _strClock; nameLen = _strClockLen; break;
        }

        WindowManager.Create(180 + item * 12, 130 + item * 10, 320, 200,
            name, nameLen,
            WindowManager.WfVisible | WindowManager.WfHasClose | WindowManager.WfSystemMenu,
            WindowManager.WkApplication);
    }

    // Mirrors the item grid painted by PaintProgmanClient.
    private static int ProgmanItemAt(int x, int y)
    {
        if (_progmanWindow < 0) return -1;
        WindowManager.GetClientArea(_progmanWindow, out int cx, out int cy, out int cw, out int ch);

        int col = (x - (cx + 12)) / 150;
        int row = (y - (cy + 26)) / 44;
        if (col < 0 || col > 1 || row < 0 || row > 2) return -1;
        if ((x - (cx + 12)) < 0 || (y - (cy + 26)) < 0) return -1;
        if (col * 150 > cw) return -1;

        return row * 2 + col;
    }

    private static void CloseActiveWindow()
    {
        int a = WindowManager.GetActiveIndex();
        if (a >= 0) CloseWindow(a);
    }

    private static void CloseWindow(int index)
    {
        if (index == _progmanWindow)
        {
_running = true; // never exit
            return;
        }
        WindowManager.Destroy(index);
    }

    private static int Abs(int v) => v < 0 ? -v : v;
}
