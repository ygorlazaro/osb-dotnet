// OSTK - desktop input: mouse and keyboard handling. Single entry point
// ProcessInput(), called by OSTK.App each frame.

namespace Osb.Toolkit;

internal static unsafe partial class Desktop
{
    private const int DoubleClickTicks = 90;

    private static bool _mouseLeftDown, _mouseRightDown;

    public static bool ProcessInput()
    {
        bool leftNow = Osb.Xwin.Mouse.LeftButton;
        bool rightNow = Osb.Xwin.Mouse.RightButton;
        bool changed = false;

        if (leftNow && !_mouseLeftDown)
        {
            HandleMouseDown(Osb.Xwin.Mouse.X, Osb.Xwin.Mouse.Y, 1);
            _mouseLeftDown = true;
            changed = true;
        }
        else if (!leftNow && _mouseLeftDown)
        {
            HandleMouseUp(Osb.Xwin.Mouse.X, Osb.Xwin.Mouse.Y, 1);
            _mouseLeftDown = false;
        }

        if (rightNow && !_mouseRightDown)
            _mouseRightDown = true;
        else if (!rightNow && _mouseRightDown)
            _mouseRightDown = false;

        if (Osb.Xwin.WindowManager.IsCapturing && (leftNow || rightNow))
            Osb.Xwin.WindowManager.UpdateCapture(Osb.Xwin.Mouse.X, Osb.Xwin.Mouse.Y);

        ProcessKeyboard();
        return changed;
    }

    private static void HandleMouseDown(int x, int y, int button)
    {
        if (button != 1) return;

        if (x < DeskbarWidth)
        {
            if (y >= ShutdownButtonY() && y < ShutdownButtonY() + MenuButtonH - 4)
            {
                _shutdownOpen = true;
                _shutdownSelected = 3;
                return;
            }

            if (y >= 6 && y < 6 + MenuButtonH - 4)
            {
                if (_menuOpen) _menuOpen = false;
                else { _menuOpen = true; _menuSelected = 0; }
                return;
            }

            int win = WindowButtonAt(x, y);
            if (win >= 0)
            {
                if (_menuOpen) _menuOpen = false;
                Window.BringToFront(win);
                return;
            }

            return;
        }

        if (_menuOpen) { _menuOpen = false; return; }

        int hit = Osb.Xwin.WindowManager.WindowAt(x, y);
        if (hit >= 0)
        {
            int ht = Osb.Xwin.WindowManager.HitTest(hit, x, y);
            if (ht == Osb.Xwin.WindowManager.HtClose)
            {
                if (hit == _progmanWindow) return;
                Window.Destroy(hit);
                return;
            }

            if (ht == Osb.Xwin.WindowManager.HtCaption || ht == Osb.Xwin.WindowManager.HtSysMenu)
            {
                Osb.Xwin.WindowManager.BeginCapture(hit, Osb.Xwin.WindowManager.HtCaption, x, y);
                return;
            }

            if (ht == Osb.Xwin.WindowManager.HtMaximize)
            {
                Osb.Xwin.WindowManager.Maximize(hit, 0);
                return;
            }

            if (ht == Osb.Xwin.WindowManager.HtMinimize)
            {
                Osb.Xwin.WindowManager.Minimize(hit);
                return;
            }

            if (ht >= Osb.Xwin.WindowManager.HtLeft && ht <= Osb.Xwin.WindowManager.HtBottomRight)
            {
                Osb.Xwin.WindowManager.BeginCapture(hit, ht, x, y);
                return;
            }

            if (ht == Osb.Xwin.WindowManager.HtClient)
            {
                Window.BringToFront(hit);
                int chit = Controls.HitControl(x, y, hit);
                if (chit >= 0) OnWindowControl(hit, chit);
                return;
            }
            return;
        }

        OnDesktopBackground(x, y);
    }

    private static void HandleMouseUp(int x, int y, int button)
    {
        Osb.Xwin.WindowManager.EndCapture();
    }

    private static void ProcessKeyboard()
    {
        if (_shutdownOpen)
        {
            int sc;
            while ((sc = Osb.Xwin.Keyboard.Pop()) >= 0)
            {
                if (sc == 0x01) _shutdownOpen = false;
                else if (sc == 0x1C) ActivateShutdown(_shutdownSelected);
                else if (sc == 0x48) { _shutdownSelected--; if (_shutdownSelected < 0) _shutdownSelected = 3; }
                else if (sc == 0x50) { _shutdownSelected++; if (_shutdownSelected > 3) _shutdownSelected = 0; }
            }
            return;
        }

        if (_menuOpen)
        {
            int sc;
            while ((sc = Osb.Xwin.Keyboard.Pop()) >= 0)
            {
                if (sc == 0x01) _menuOpen = false;
                else if (sc == 0x1C)
                {
                    if (_appCount > 0)
                    {
                        OpenAppFromIndex(_appIconIdx[_menuSelected]);
                        _menuOpen = false;
                    }
                }
                else if (sc == 0x48) { _menuSelected--; if (_menuSelected < 0) _menuSelected = _appCount - 1; }
                else if (sc == 0x50) { _menuSelected++; if (_menuSelected >= _appCount) _menuSelected = 0; }
            }
            return;
        }

        if (Osb.Xwin.Keyboard.ConsumeCtrlF4())
        {
            int a = Osb.Xwin.WindowManager.GetActiveIndex();
            if (a >= 0 && a != _progmanWindow) Window.Destroy(a);
            return;
        }

        while (Osb.Xwin.Keyboard.Pop() >= 0) { }
    }

    private static void ActivateShutdown(int selected)
    {
        switch (selected)
        {
            case 0: Osb.Xwin.Sys.PowerOff(); break;
            case 1: Osb.Xwin.Sys.Reboot(); break;
        }
        _shutdownOpen = false;
    }

    private static void ProcessShutdownClick(int x, int y)
    {
        int dx = ShutdownDialogX();
        int dy = ShutdownDialogY();
        int oy = dy + 30;
        for (int i = 0; i < 4; i++)
        {
            if (x >= dx + 10 && x < dx + ShutdownDialogW - 10 &&
                y >= oy && y < oy + 16)
            {
                _shutdownSelected = i;
                ActivateShutdown(i);
                return;
            }
            oy += 22;
        }
    }

    private static void ProcessMenuClick(int x, int y)
    {
        int mx = DeskbarWidth + 4;
        int my = 6 + MenuButtonH;
        const int itemH = 18;
        for (int i = 0; i < _appCount; i++)
        {
            int iy = my + 4 + i * itemH;
            if (x >= mx + 2 && x < mx + 118 && y >= iy && y < iy + itemH - 2)
            {
                OpenAppFromIndex(_appIconIdx[i]);
                _menuOpen = false;
                return;
            }
        }
    }
}