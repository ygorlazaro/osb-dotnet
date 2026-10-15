// Desktop shell - Windows 3.11 style Program Manager desktop.
//
// Runs in graphics mode, manages the desktop background, taskbar,
// program groups, and window management. The message loop lives here;
// input handling is in Desktop.Input.cs and painting in Desktop.Paint.cs.

using Osb.Kernel.Cpu;
using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;
using Osb.Kernel.Shell;

namespace Osb.Kernel.Shell;

internal static unsafe partial class Desktop
{
    // Desktop state
    private static bool _running;
    private static int _lastMouseX, _lastMouseY;
    private static bool _mouseLeftDown, _mouseRightDown;
    private static int _lastClickTime;
    private static int _lastClickX, _lastClickY;
    private static int _lastClickButton;

    // Taskbar
    private const int TaskbarHeight = 20;
    private static int _taskbarStartX = 4;
    private static int _startButtonW = 60;

    // Program Manager window
    private static int _progmanWindow = -1;

    // Static string pointers for painting (allocated once)
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
    private static int _strMainLen, _strAccessoriesLen, _strGamesLen, _strStartupLen;
    private static int _strWriteLen, _strPaintbrushLen, _strCalculatorLen, _strNotepadLen;
    private static int _strTerminalLen, _strClockLen, _strAppWindowLen, _strCloseMsgLen;
    private static int _strGroupWindowLen, _strGroupMsgLen, _strStartLen;

    // Icon grid
    private struct Icon
    {
        public char* name;
        public int nameLen;
        public int x, y;
        public byte iconType;  // 0=program, 1=group, 2=document
        public char* command;  // Command to run
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
        if (!Gfx.Active)
        {
            Gfx.Start(Gfx.Width, Gfx.Height);
            Gfx.Enter();
        }

        InitDesktop();
        _running = true;

        // Main message loop
        while (_running)
        {
            // Process mouse
            ProcessMouse();

            // Process keyboard
            ProcessKeyboard();

            // Paint
            PaintDesktop();

            // Draw cursor
            Cursor.Draw(Mouse.X, Mouse.Y);

            // Small delay (~10ms = 10 ticks at 100Hz)
            ulong now = Pit.Ticks;
            while (Pit.Ticks - now < 10) { NativeMethods.hlt_cpu(); }
        }

        // Cleanup
        Cursor.Restore();
        Gfx.Exit();
        Vga.Clear();
    }

    private static void InitDesktop()
    {
        // Initialize window manager
        WindowManager.Init();

        // Allocate static strings for painting
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

        // Set up desktop background
        Gfx.Clear(Palette.Desktop);

        // Create program groups/icons
        SetupIcons();

        // Create Program Manager window
        CreateProgramManager();

        // Draw initial state
        PaintDesktop();
        Cursor.Draw(Mouse.X, Mouse.Y);
    }

    private static void SetupIcons()
    {
        _iconCount = 8;
        _icons = (Icon*)Kernel.OsAlloc(sizeof(Icon) * _iconCount);

        // Calculate grid
        _iconCols = (Gfx.Width - IconStartX) / (IconWidth + IconSpacingX);
        _iconRows = (Gfx.Height - TaskbarHeight - IconStartY) / (IconHeight + IconSpacingY + 16);  // +16 for label

        int idx = 0;

        // Add program group icons (Windows 3.11 style)
        _icons[idx++] = CreateIcon("Main", "MAIN.GRP", 1);
        _icons[idx++] = CreateIcon("Accessories", "ACCESS.GRP", 1);
        _icons[idx++] = CreateIcon("Games", "GAMES.GRP", 1);
        _icons[idx++] = CreateIcon("Startup", "STARTUP.GRP", 1);

        // Add program icons
        _icons[idx++] = CreateIcon("Write", "WRITE", 0);
        _icons[idx++] = CreateIcon("Paint", "PAINT", 0);
        _icons[idx++] = CreateIcon("Calc", "CALC", 0);
        _icons[idx++] = CreateIcon("Notepad", "NOTEPAD", 0);

        // Position icons in grid
        for (int i = 0; i < _iconCount; i++)
        {
            int col = i % _iconCols;
            int row = i / _iconCols;
            _icons[i].x = IconStartX + col * (IconWidth + IconSpacingX);
            _icons[i].y = IconStartY + row * (IconHeight + IconSpacingY + 16);
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
        len = s.Length;
        char* ptr = (char*)Kernel.OsAlloc((len + 1) * 2);
        fixed (char* src = s)
        {
            for (int i = 0; i <= len; i++)
                ptr[i] = src[i];
        }
        return ptr;
    }

    private static void CreateProgramManager()
    {
        // Create a sample Program Manager window
        char* title = AllocString("Program Manager", out int titleLen);
        _progmanWindow = WindowManager.Create(
            50, 50, 400, 300,
            title, titleLen,
            WindowManager.WfVisible | WindowManager.WfActive | WindowManager.WfHasClose | WindowManager.WfHasMax | WindowManager.WfHasMin | WindowManager.WfSystemMenu,
            WindowManager.WkProgman
        );
    }
}
