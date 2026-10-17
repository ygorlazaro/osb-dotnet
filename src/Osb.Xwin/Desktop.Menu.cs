// XWIN desktop - app menu, shutdown dialog, and program launching.
//
// Split out of Desktop.Input.cs (400-line rule); shares the desktop's static
// state through the partial class.

namespace Osb.Xwin;

internal static unsafe partial class Desktop
{
    // ---- app menu (installed apps) ----

    private static void ProcessMenuMouse()
    {
        int mx = DeskbarWidth + 4;
        int my = 6 + MenuButtonH;
        const int itemH = 18;
        const int mw = 120;

        int sel = -1;
        for (int i = 0; i < _appCount; i++)
        {
            int iy = my + 4 + i * itemH;
            if (Mouse.X >= mx && Mouse.X < mx + mw - 4 &&
                Mouse.Y >= iy && Mouse.Y < iy + itemH - 2)
            {
                sel = i;
                break;
            }
        }
        if (sel >= 0) _menuSelected = sel;
    }

    private static void ProcessMenuClick(int x, int y)
    {
        int mx = DeskbarWidth + 4;
        int my = 6 + MenuButtonH;
        const int itemH = 18;
        const int mw = 120;

        for (int i = 0; i < _appCount; i++)
        {
            int iy = my + 4 + i * itemH;
            if (x >= mx && x < mx + mw - 4 && y >= iy && y < iy + itemH - 2)
            {
                OpenIcon(_appIconIdx[i]);
                _menuOpen = false;
                return;
            }
        }
        _menuOpen = false; // click outside closes the menu
    }

    // ---- shutdown confirmation dialog ----

    private static void ProcessShutdownMouse()
    {
        int dx = ShutdownDialogX();
        int dy = ShutdownDialogY();
        int oy = dy + 30;

        int sel = -1;
        for (int i = 0; i < 4; i++)
        {
            if (Mouse.X >= dx + 10 && Mouse.X < dx + ShutdownDialogW - 10 &&
                Mouse.Y >= oy && Mouse.Y < oy + 16)
            {
                sel = i;
                break;
            }
            oy += 22;
        }
        if (sel >= 0) _shutdownSelected = sel;
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
                ActivateShutdown(i);
                return;
            }
            oy += 22;
        }
        ActivateShutdown(3); // click outside cancels
    }

    private static void ActivateShutdown(int sel)
    {
        _shutdownOpen = false;
        switch (sel)
        {
            case 0: Sys.PowerOff(); break;   // ACPI S5; never returns
            case 1: Sys.Reboot(); break;     // 8042 reset; never returns
            case 2: _running = false; break; // back to terminal
            case 3: break;                   // cancel
        }
    }

    // ---- program launching ----

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
            case 5: name = _strLocator; nameLen = _strLocatorLen; break;
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
}
