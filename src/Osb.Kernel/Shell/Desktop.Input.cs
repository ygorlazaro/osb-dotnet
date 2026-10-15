// Desktop shell - mouse and keyboard input handling.
//
// Split out of Desktop.cs (400-line rule); shares the desktop's static state
// through the partial class.

using Osb.Kernel.Cpu;
using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell;

internal static unsafe partial class Desktop
{
    private static void ProcessMouse()
    {
        _lastMouseX = Mouse.X;
        _lastMouseY = Mouse.Y;

        // Check for button state changes
        bool leftNow = Mouse.LeftButton;
        bool rightNow = Mouse.RightButton;

        // Handle mouse down/up
        if (leftNow && !_mouseLeftDown)
        {
            // Left button pressed
            HandleMouseDown(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = true;
        }
        else if (!leftNow && _mouseLeftDown)
        {
            // Left button released
            HandleMouseUp(Mouse.X, Mouse.Y, 1);
            _mouseLeftDown = false;
        }

        if (rightNow && !_mouseRightDown)
        {
            _mouseRightDown = true;
        }
        else if (!rightNow && _mouseRightDown)
        {
            _mouseRightDown = false;
        }

        // Handle capture (drag/size)
        if (WindowManager.IsCapturing)
        {
            WindowManager.UpdateCapture(Mouse.X, Mouse.Y);
        }
    }

    private static void HandleMouseDown(int x, int y, int button)
    {
        // Check taskbar first
        if (y >= Gfx.Height - TaskbarHeight)
        {
            HandleTaskbarClick(x, y);
            return;
        }

        // Check desktop icons
        int iconIdx = HitTestIcon(x, y);
        if (iconIdx >= 0)
        {
            // Check for double-click
            int now = (int)Pit.Ticks;
            if (now - _lastClickTime < 30 &&  // ~300ms at 100Hz
                x == _lastClickX && y == _lastClickY && button == _lastClickButton)
            {
                // Double-click - launch
                LaunchIcon(iconIdx);
            }
            else
            {
                // Single click - select
                // Highlight icon
            }
            _lastClickTime = now;
            _lastClickX = x;
            _lastClickY = y;
            _lastClickButton = button;
            return;
        }

        // Check windows
        int winIdx = WindowManager.WindowAt(x, y);
        if (winIdx >= 0)
        {
            int hit = WindowManager.HitTest(winIdx, x, y);
            if (hit != WindowManager.HtNowhere && hit != WindowManager.HtClient)
            {
                WindowManager.BeginCapture(winIdx, hit, x, y);
            }
            else
            {
                WindowManager.BringToFront(winIdx);
            }
            return;
        }

        // Clicked on desktop background - deselect
    }

    private static void HandleMouseUp(int x, int y, int button)
    {
        if (WindowManager.IsCapturing)
        {
            WindowManager.EndCapture();
        }
    }

    private static int HitTestIcon(int x, int y)
    {
        for (int i = 0; i < _iconCount; i++)
        {
            Icon* ic = &_icons[i];
            if (x >= ic->x && x < ic->x + IconWidth &&
                y >= ic->y && y < ic->y + IconHeight + 16)
            {
                return i;
            }
        }
        return -1;
    }

    private static void LaunchIcon(int index)
    {
        if (index < 0 || index >= _iconCount) return;

        Icon* ic = &_icons[index];

        // For now, just open a simple window
        if (ic->iconType == 0)  // Program
        {
            CreateAppWindow(ic->name, ic->nameLen);
        }
        else if (ic->iconType == 1)  // Group
        {
            // Open group window
            CreateGroupWindow(ic->name, ic->nameLen);
        }
    }

    private static void CreateAppWindow(char* name, int nameLen)
    {
        WindowManager.Create(
            100 + (WindowManager.WindowCount * 20) % 200,
            100 + (WindowManager.WindowCount * 20) % 150,
            320, 240,
            name, nameLen,
            WindowManager.WfVisible | WindowManager.WfActive | WindowManager.WfHasClose | WindowManager.WfHasMax | WindowManager.WfHasMin | WindowManager.WfSystemMenu,
            WindowManager.WkApplication
        );
    }

    private static void CreateGroupWindow(char* name, int nameLen)
    {
        WindowManager.Create(
            150, 100, 300, 250,
            name, nameLen,
            WindowManager.WfVisible | WindowManager.WfActive | WindowManager.WfHasClose | WindowManager.WfHasMax | WindowManager.WfHasMin | WindowManager.WfSystemMenu,
            WindowManager.WkGroup
        );
    }

    private static void HandleTaskbarClick(int x, int y)
    {
        // Start button
        if (x >= _taskbarStartX && x < _taskbarStartX + _startButtonW)
        {
            // Toggle start menu - not implemented yet
            return;
        }

        // Task buttons - not implemented yet
    }

    private static void ProcessKeyboard()
    {
        // Ctrl+F4: close the active window, or exit the desktop if that window
        // is the Program Manager.
        if (Keyboard.ConsumeCtrlF4())
        {
            int active = WindowManager.GetActiveIndex();
            if (active >= 0)
            {
                if (WindowManager.GetKind(active) == WindowManager.WkProgman)
                {
                    _running = false;
                    return;
                }
                WindowManager.Destroy(active);
            }
            return;
        }

        int sc = Keyboard.Pop();
        if (sc < 0) return;

        // Check for Ctrl+Esc (Start menu)
        if (Keyboard.IsCtrlPressed && sc == 0x01)  // Esc
        {
            // Start menu
            return;
        }

        // Pass to active window if any
        int activeIdx = WindowManager.GetActiveIndex();
        if (activeIdx >= 0)
        {
            // Could send keystroke to window
        }
    }
}
