// XWIN desktop - core mouse and keyboard input handling.
//
// Split out of Desktop.cs (400-line rule); shares the desktop's static state
// through the partial class. Ported from the kernel's Shell/Desktop.Input.cs.

namespace Osb.Xwin;

internal static unsafe partial class Desktop
{
    private const int DoubleClickTicks = 90;   // ~900 ms at 100 Hz

    private static void ProcessMouse()
    {
        _lastMouseX = Mouse.X;
        _lastMouseY = Mouse.Y;

        bool leftNow = Mouse.LeftButton;
        bool rightNow = Mouse.RightButton;

        // Left-button down edge: modal overlays consume the click first.
        if (leftNow && !_mouseLeftDown)
        {
            if (_shutdownOpen)
                ProcessShutdownClick(Mouse.X, Mouse.Y);
            else if (_menuOpen)
                ProcessMenuClick(Mouse.X, Mouse.Y);
            else
                HandleMouseDown(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = true;
        }
        else if (!leftNow && _mouseLeftDown)
        {
            HandleMouseUp(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = false;
        }

        if (rightNow && !_mouseRightDown)
            _mouseRightDown = true;
        else if (!rightNow && _mouseRightDown)
            _mouseRightDown = false;

        // Keep dragging/sizing while a button is held.
        if (WindowManager.IsCapturing && (leftNow || rightNow))
            WindowManager.UpdateCapture(Mouse.X, Mouse.Y);

        // Modal overlays track the pointer for hover highlighting.
        if (_shutdownOpen)
            ProcessShutdownMouse();
        else if (_menuOpen)
            ProcessMenuMouse();
    }

    private static void HandleMouseDown(int x, int y, int button)
    {
        if (button != 1) return;

        // Left deskbar (top-left anchored). It sits on top of everything, so
        // clicks here never fall through to windows/icons.
        if (x < DeskbarWidth)
        {
            // Shutdown button at the bottom end.
            if (y >= ShutdownButtonY() && y < ShutdownButtonY() + MenuButtonH - 4)
            {
                _shutdownOpen = true;
                _shutdownSelected = 3; // default to Cancel
                return;
            }

            // Menu button at top: first button, opens the installed-apps menu.
            if (y >= 6 && y < 6 + MenuButtonH - 4)
            {
                if (_menuOpen) _menuOpen = false;
                else { _menuOpen = true; _menuSelected = 0; }
                return;
            }

            // Stacked window buttons below the menu button.
            int win = WindowButtonAt(x, y);
            if (win >= 0)
            {
                if (_menuOpen) _menuOpen = false;
                WindowManager.BringToFront(win);
                return;
            }

            return;
        }

        // Click outside the deskbar closes an open menu.
        if (_menuOpen) { _menuOpen = false; return; }

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

            if (ht == WindowManager.HtMaximize)
            {
                WindowManager.Maximize(hit, 0);
                return;
            }

            if (ht == WindowManager.HtMinimize)
            {
                WindowManager.Minimize(hit);
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
                   Abs(x - _lastClickX) < 12 &&
                   Abs(y - _lastClickY) < 12;

        _lastClickTime = now;
        _lastClickX = x;
        _lastClickY = y;
        _lastClickButton = button;
        return dbl;
    }

    private static void ProcessKeyboard()
    {
        if (_shutdownOpen)
        {
            int sc;
            while ((sc = Keyboard.Pop()) >= 0)
            {
                if (sc == 0x01)              // Esc: cancel
                    _shutdownOpen = false;
                else if (sc == 0x1C)         // Enter: activate
                    ActivateShutdown(_shutdownSelected);
                else if (sc == 0x48)         // Up
                {
                    _shutdownSelected--;
                    if (_shutdownSelected < 0) _shutdownSelected = 3;
                }
                else if (sc == 0x50)         // Down
                {
                    _shutdownSelected++;
                    if (_shutdownSelected > 3) _shutdownSelected = 0;
                }
            }
            return;
        }

        if (_menuOpen)
        {
            int sc;
            while ((sc = Keyboard.Pop()) >= 0)
            {
                if (sc == 0x01)              // Esc: close menu
                    _menuOpen = false;
                else if (sc == 0x1C)         // Enter: launch selected app
                {
                    if (_appCount > 0)
                    {
                        OpenIcon(_appIconIdx[_menuSelected]);
                        _menuOpen = false;
                    }
                }
                else if (sc == 0x48)         // Up
                {
                    _menuSelected--;
                    if (_menuSelected < 0) _menuSelected = _appCount - 1;
                }
                else if (sc == 0x50)         // Down
                {
                    _menuSelected++;
                    if (_menuSelected >= _appCount) _menuSelected = 0;
                }
            }
            return;
        }

        if (_frameCount > 100 && Keyboard.ConsumeCtrlF4())
        {
            CloseActiveWindow();
            return;
        }

        int sc2;
        while ((sc2 = Keyboard.Pop()) >= 0)
        {
            if (sc2 == 0x01)              // Esc: clear the icon selection
                _selectedIcon = -1;
        }
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
