// XWIN desktop - a Windows 3.11 style Program Manager.
//
// Ported from the kernel's built-in Shell/Desktop.cs so the external XWIN
// program shows exactly what the built-in desktop did, but drives the screen
// through INT 21h instead of touching hardware directly.

namespace Osb.Xwin;

internal static unsafe partial class Desktop
{
    private static bool _running;
    private static int _lastMouseX, _lastMouseY;
    private static bool _mouseLeftDown, _mouseRightDown;
    private static int _lastClickTime;
    private static int _lastClickX, _lastClickY;
    private static int _lastClickButton;

    // Left vertical deskbar (Haiku-style): top-left anchored, full height.
    private const int DeskbarWidth = 72;
    private const int MenuButtonH = 24;
    private const int PanelButtonH = 20;

    private static int _progmanWindow = -1;
    private static int _frameCount;

    // App menu state (opened from the deskbar's first button).
    private static bool _menuOpen;
    private static int _menuSelected;
    private static int* _appIconIdx;  // indices into _icons for type-0 (app) icons
    private static int _appCount;

    // Shutdown confirmation dialog state.
    private static bool _shutdownOpen;
    private static int _shutdownSelected; // 0=turn off, 1=reboot, 2=back to terminal, 3=cancel

    private static int _selectedIcon = -1;

    private static char* _strFile;
    private static int _strFileLen;
    private static char* _strOptions;
    private static int _strOptionsLen;
    private static char* _strWindow;
    private static int _strWindowLen;
    private static char* _strHelp;
    private static int _strHelpLen;
    private static char* _strMain;
    private static char* _strAccessories;
    private static char* _strGames;
    private static char* _strStartup;
    private static char* _strWrite;
    private static char* _strPaintbrush;
    private static char* _strCalculator;
    private static char* _strNotepad;
    private static char* _strTerminal;
    private static char* _strClock;
    private static char* _strAppWindow;
    private static char* _strCloseMsg;
    private static char* _strGroupWindow;
    private static char* _strGroupMsg;
    private static char* _strStart;
    private static char* _strOSB;
    private static int _strOSBLen;
    private static char* _strShutDown;
    private static int _strShutDownLen;
    private static char* _strShutDownTitle;
    private static int _strShutDownTitleLen;
    private static int _strMainLen, _strAccessoriesLen, _strGamesLen, _strStartupLen;
    private static int _strWriteLen, _strPaintbrushLen, _strCalculatorLen, _strNotepadLen;
    private static int _strTerminalLen, _strClockLen, _strAppWindowLen, _strCloseMsgLen;
    private static int _strGroupWindowLen, _strGroupMsgLen, _strStartLen;

    private struct Icon
    {
        public char* name;
        public int nameLen;
        public int x, y;
        public byte iconType;
        public char* command;
        public int commandLen;
    }

    private static Icon* _icons;
    private static int _iconCount;
    private static int _iconCols;
    private static int _iconRows;
    private const int IconWidth = 64;
    private const int IconHeight = 64;
    private const int IconSpacingX = 8;
    private const int IconSpacingY = 8;
    private const int IconStartX = 16;
    private const int IconStartY = 16;

    public static void Run()
    {
        if (!Gfx.Active) { Gfx.Start(); Gfx.Enter(); }

        InitDesktop();
        _running = true;

        while (_running)
        {
            _frameCount++;
            // Erase the cursor's old pixels before the frame is repainted, so
            // the repaint is not corrupted by a stale saved background.
            Cursor.Restore();

            Mouse.Refresh();
            Keyboard.Refresh();

            int layoutBefore = WindowManager.LayoutVersion;
            int selectedIconBefore = _selectedIcon;

            ProcessMouse();
            ProcessKeyboard();

            // Conditional repaint: redraw whenever mouse/keyboard input changed
            // visible state (window z-order, open/close, menu, icon selection).
            bool inputChanged = WindowManager.LayoutVersion != layoutBefore ||
                                _selectedIcon != selectedIconBefore;

            bool needPaint = _frameCount == 1 || inputChanged;
            if (needPaint)
                PaintDesktop();

            if (_running) Cursor.Draw(Mouse.X, Mouse.Y);

            Pit.Sleep(5);   // ~20 frames/s at 100 Hz (smoother under emulation)
        }

        Cursor.Restore();
        Gfx.Exit();
    }

    private static void InitDesktop()
    {
        WindowManager.Init();

        _strFile = AllocString("File", out _strFileLen);
        _strOptions = AllocString("Options", out _strOptionsLen);
        _strWindow = AllocString("Window", out _strWindowLen);
        _strHelp = AllocString("Help", out _strHelpLen);
        _strMain = AllocString("Main", out _strMainLen);
        _strAccessories = AllocString("Accessories", out _strAccessoriesLen);
        _strGames = AllocString("Games", out _strGamesLen);
        _strStartup = AllocString("Startup", out _strStartupLen);
        _strWrite = AllocString("Write", out _strWriteLen);
        _strPaintbrush = AllocString("Paintbrush", out _strPaintbrushLen);
        _strCalculator = AllocString("Calculator", out _strCalculatorLen);
        _strNotepad = AllocString("Notepad", out _strNotepadLen);
        _strTerminal = AllocString("Terminal", out _strTerminalLen);
        _strClock = AllocString("Clock", out _strClockLen);
        _strAppWindow = AllocString("Application Window", out _strAppWindowLen);
        _strCloseMsg = AllocString("Press Ctrl+F4 to close", out _strCloseMsgLen);
        _strGroupWindow = AllocString("Group Window", out _strGroupWindowLen);
        _strGroupMsg = AllocString("Double-click items to open", out _strGroupMsgLen);
        _strStart = AllocString("Start", out _strStartLen);
        _strOSB = AllocString("OSB", out _strOSBLen);
        _strShutDown = AllocString("Turn off PC", out _strShutDownLen);
        _strShutDownTitle = AllocString("Turn off PC", out _strShutDownTitleLen);

        Gfx.Clear(Palette.Desktop);

        SetupIcons();
        CreateProgramManager();

        PaintDesktop();
        Cursor.Draw(Mouse.X, Mouse.Y);
    }

    private static void SetupIcons()
    {
        _iconCount = 8;
        _icons = (Icon*)Arena.Alloc(sizeof(Icon) * _iconCount);

        _iconCols = (Gfx.Width - IconStartX - DeskbarWidth) / (IconWidth + IconSpacingX);
        _iconRows = (Gfx.Height - IconStartY) / (IconHeight + IconSpacingY + 16);

        _appIconIdx = (int*)Arena.Alloc(sizeof(int) * _iconCount);
        _appCount = 0;

        int idx = 0;
        _icons[idx++] = CreateIcon("Main", "MAIN.GRP", 1);
        _icons[idx++] = CreateIcon("Accessories", "ACCESS.GRP", 1);
        _icons[idx++] = CreateIcon("Games", "GAMES.GRP", 1);
        _icons[idx++] = CreateIcon("Startup", "STARTUP.GRP", 1);
        _icons[idx++] = CreateIcon("Write", "WRITE", 0);
        _icons[idx++] = CreateIcon("Paint", "PAINT", 0);
        _icons[idx++] = CreateIcon("Calc", "CALC", 0);
        _icons[idx++] = CreateIcon("Notepad", "NOTEPAD", 0);

        for (int i = 0; i < _iconCount; i++)
        {
            int col = i % _iconCols;
            int row = i / _iconCols;
            _icons[i].x = DeskbarWidth + IconStartX + col * (IconWidth + IconSpacingX);
            _icons[i].y = IconStartY + row * (IconHeight + IconSpacingY + 16);

            if (_icons[i].iconType == 0)
                _appIconIdx[_appCount++] = i;
        }
    }

    private static Icon CreateIcon(string name, string command, byte type)
    {
        char* namePtr = AllocString(name, out int nameLen);
        char* cmdPtr = AllocString(command, out int cmdLen);
        return new Icon { name = namePtr, nameLen = nameLen, iconType = type, command = cmdPtr, commandLen = cmdLen };
    }

    private static char* AllocString(string s, out int len)
    {
        // The freestanding runtime does not expose string.Length reliably,
        // so derive the length by scanning the pinned char data for NUL.
        len = 0;
        fixed (char* src = s)
        {
            while (len < 256 && src[len] != (char)0) len++;
        }
        char* ptr = (char*)Arena.Alloc((len + 1) * 2);
        fixed (char* src = s)
        {
            for (int i = 0; i <= len; i++)
                ptr[i] = src[i];
        }
        return ptr;
    }

    private static void CreateProgramManager()
    {
        char* title = AllocString("Program Manager", out int titleLen);
        _progmanWindow = WindowManager.Create(
            DeskbarWidth + 18, 60, 400, 300,
            title, titleLen,
            WindowManager.WfVisible | WindowManager.WfActive | WindowManager.WfHasClose |
            WindowManager.WfHasMax | WindowManager.WfHasMin | WindowManager.WfSystemMenu,
            WindowManager.WkProgman);
    }
}
