// OSTK - desktop shell. The OSB desktop (Windows 3.11 style Program Manager)
// rebuilt on top of OSTK: windows via OSTK.Window, program-manager items via
// OSTK.Controls, drawing via OSTK.Graphics/Paint and the Osb.Xwin Gfx/Font
// bindings. The message loop lives in OSTK.App.

using Osb.Xwin;

namespace Osb.Toolkit;

internal static unsafe partial class Desktop
{
    private const int DeskbarWidth = 72;
    private const int MenuButtonH = 24;
    private const int PanelButtonH = 20;

    private static int _progmanWindow = -1;

    private static bool _menuOpen;
    private static int _menuSelected;
    private static int* _appIconIdx;
    private static int _appCount;

    private static bool _shutdownOpen;
    private static int _shutdownSelected;

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
    private static int _strMainLen;
    private static char* _strAccessories;
    private static int _strAccessoriesLen;
    private static char* _strGames;
    private static int _strGamesLen;
    private static char* _strStartup;
    private static int _strStartupLen;
    private static char* _strWrite;
    private static int _strWriteLen;
    private static char* _strPaintbrush;
    private static int _strPaintbrushLen;
    private static char* _strCalculator;
    private static int _strCalculatorLen;
    private static char* _strNotepad;
    private static int _strNotepadLen;
    private static char* _strTerminal;
    private static int _strTerminalLen;
    private static char* _strClock;
    private static int _strClockLen;
    private static char* _strAppWindow;
    private static int _strAppWindowLen;
    private static char* _strCloseMsg;
    private static int _strCloseMsgLen;
    private static char* _strGroupWindow;
    private static int _strGroupWindowLen;
    private static char* _strGroupMsg;
    private static int _strGroupMsgLen;
    private static char* _strStart;
    private static int _strStartLen;
    private static char* _strOSB;
    private static int _strOSBLen;
    private static char* _strShutDown;
    private static int _strShutDownLen;
    private static char* _strShutDownTitle;
    private static int _strShutDownTitleLen;

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
    private const int IconWidth = 64;
    private const int IconHeight = 64;
    private const int IconSpacingX = 8;
    private const int IconSpacingY = 8;
    private const int IconStartX = 16;
    private const int IconStartY = 16;

    public static void InitDesktop()
    {
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

        // The deskbar is painted over the windows: keep them out from under it.
        Osb.Xwin.WindowManager.WorkAreaLeft = DeskbarWidth + 1;

        SetupAppMenu();
        CreateProgramManager();
    }

    private static char* AllocString(string s, out int len)
    {
        len = 0;
        fixed (char* src = s)
        {
            while (len < 256 && src[len] != (char)0) len++;
        }
        char* ptr = (char*)Osb.Xwin.Arena.Alloc((len + 1) * 2);
        fixed (char* src = s)
        {
            for (int i = 0; i <= len; i++) ptr[i] = src[i];
        }
        return ptr;
    }

    private static void SetupIcons()
    {
        _iconCount = 8;
        _icons = (Icon*)Osb.Xwin.Arena.Alloc(sizeof(Icon) * _iconCount);

        _iconCols = (Osb.Xwin.Gfx.Width - IconStartX - DeskbarWidth) / (IconWidth + IconSpacingX);

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
        }
    }

    // The deskbar's app menu lists the Program Manager programs. _appIconIdx
    // holds program numbers (what OpenAppFromIndex takes), not desktop icon
    // indices, so the menu works whether or not SetupIcons ever runs.
    private const int AppMenuCount = 6;

    private static void SetupAppMenu()
    {
        _appCount = AppMenuCount;
        _appIconIdx = (int*)Osb.Xwin.Arena.Alloc(sizeof(int) * AppMenuCount);
        for (int i = 0; i < AppMenuCount; i++) _appIconIdx[i] = i;
    }

    private static char* AppName(int app, out int len)
    {
        switch (app)
        {
            case 0: len = _strWriteLen; return _strWrite;
            case 1: len = _strPaintbrushLen; return _strPaintbrush;
            case 2: len = _strCalculatorLen; return _strCalculator;
            case 3: len = _strNotepadLen; return _strNotepad;
            case 4: len = _strTerminalLen; return _strTerminal;
            default: len = _strClockLen; return _strClock;
        }
    }

    private static Icon CreateIcon(string name, string command, byte type)
    {
        char* namePtr = AllocString(name, out int nameLen);
        char* cmdPtr = AllocString(command, out int cmdLen);
        return new Icon { name = namePtr, nameLen = nameLen, iconType = type, command = cmdPtr, commandLen = cmdLen };
    }

    private static void CreateProgramManager()
    {
        char* title = AllocString("Program Manager", out int titleLen);
        _progmanWindow = Window.Create(
            DeskbarWidth + 18, 60, 400, 300,
            title, titleLen,
            WindowStyle.Visible | WindowStyle.Active | WindowStyle.HasClose |
            WindowStyle.HasMax | WindowStyle.HasMin | WindowStyle.SystemMenu);

        // Program-manager item buttons (OWNED by the progman window).
        int cx = DeskbarWidth + 18 + 12;
        int cy = 60 + 26;
        Controls.Add(ControlKind.Button, new Rect { X = cx, Y = cy, W = 130, H = 36 },
            _strWrite, _strWriteLen, _progmanWindow, 0);
        Controls.Add(ControlKind.Button, new Rect { X = cx + 150, Y = cy, W = 130, H = 36 },
            _strPaintbrush, _strPaintbrushLen, _progmanWindow, 1);
        Controls.Add(ControlKind.Button, new Rect { X = cx, Y = cy + 44, W = 130, H = 36 },
            _strCalculator, _strCalculatorLen, _progmanWindow, 2);
        Controls.Add(ControlKind.Button, new Rect { X = cx + 150, Y = cy + 44, W = 130, H = 36 },
            _strNotepad, _strNotepadLen, _progmanWindow, 3);
        Controls.Add(ControlKind.Button, new Rect { X = cx, Y = cy + 88, W = 130, H = 36 },
            _strTerminal, _strTerminalLen, _progmanWindow, 4);
        Controls.Add(ControlKind.Button, new Rect { X = cx + 150, Y = cy + 88, W = 130, H = 36 },
            _strClock, _strClockLen, _progmanWindow, 5);
    }

    public static void PaintBackground()
    {
        DrawLeftPanel();
        if (_shutdownOpen) DrawShutdownDialog();
        else if (_menuOpen) DrawAppMenu();
    }

    public static void PaintIcons()
    {
        for (int i = 0; i < _iconCount; i++)
        {
            Icon* ic = &_icons[i];
            bool sel = i == _selectedIcon;
            if (sel)
                Graphics.FillRect(ic->x - 2, ic->y - 2, IconWidth + 4, IconHeight + 22, Palette.HighlightBg);
            byte bg = sel ? Palette.HighlightBg : Palette.Desktop;
            byte fg = sel ? Palette.Highlight : Palette.IconText;
            DrawIcon32(ic->x + 16, ic->y + 8, ic->iconType, bg);
            DrawIconLabel(ic);
        }
    }

    public static void PaintWindowClient(int index, int cx, int cy, int cw, int ch)
    {
        int kind = Osb.Xwin.WindowManager.GetKind(index);
        DrawMenuBar(cx, cy, cw);

        if (kind == Osb.Xwin.WindowManager.WkProgman)
        {
            int mx = cx + 8;
            mx = Graphics.DrawString(mx, cy + 4, _strFile, _strFileLen, Palette.MenuText, Palette.WindowFace) + 14;
            mx = Graphics.DrawString(mx, cy + 4, _strOptions, _strOptionsLen, Palette.MenuText, Palette.WindowFace) + 14;
            Graphics.DrawString(mx, cy + 4, _strWindow, _strWindowLen, Palette.MenuText, Palette.WindowFace);
            Graphics.DrawString(mx + 14, cy + 4, _strHelp, _strHelpLen, Palette.MenuText, Palette.WindowFace);

            // The program-item buttons are painted by App.Render (Controls.PaintOwner).
            int lineY = cy + ch - 16;
            if (lineY > cy + 130)
                Graphics.DrawString(cx + 12, lineY, _strCloseMsg, _strCloseMsgLen, Palette.DarkGray, Palette.WindowFace);
        }
        else if (kind == Osb.Xwin.WindowManager.WkGroup)
        {
            Graphics.DrawString(cx + 12, cy + ch - 16, _strGroupMsg, _strGroupMsgLen, Palette.DarkGray, Palette.WindowFace);
        }
        else
        {
            PaintAppClient(cx, cy, cw, ch);
        }
    }

    private static void DrawMenuBar(int cx, int cy, int cw)
    {
        Graphics.FillRect(cx, cy, cw, 14, Palette.WindowFace);
        Graphics.HLine(cx, cy + 13, cw, Palette.DarkGray);
    }

    private static void PaintAppClient(int cx, int cy, int cw, int ch)
    {
        int tx = cx + (cw - _strAppWindowLen * Osb.Xwin.Font.GlyphWidth) / 2;
        if (tx < cx + 4) tx = cx + 4;
        Graphics.DrawString(tx, cy + ch / 2 - 12, _strAppWindow, _strAppWindowLen, Palette.Black, Palette.WindowFace);

        int hx = cx + (cw - _strCloseMsgLen * Osb.Xwin.Font.GlyphWidth) / 2;
        if (hx < cx + 4) hx = cx + 4;
        Graphics.DrawString(hx, cy + ch / 2 + 4, _strCloseMsg, _strCloseMsgLen, Palette.DarkGray, Palette.WindowFace);
    }

    public static void OnDesktopControl(int controlIndex) { }

    public static void OnWindowControl(int windowIndex, int controlIndex)
    {
        if (windowIndex != _progmanWindow) return;
        Control* c = Controls.Ptr(controlIndex);
        int app = c->UserData;
        OpenAppFromIndex(app);
    }

    public static void OnDesktopBackground(int x, int y)
    {
        if (_menuOpen) { _menuOpen = false; return; }
        int icon = IconAt(x, y);
        _selectedIcon = icon;
    }

    private static void OpenAppFromIndex(int app)
    {
        switch (app)
        {
            case 0: CreateAppWindow(_strWrite, _strWriteLen); break;
            case 1: CreateAppWindow(_strPaintbrush, _strPaintbrushLen); break;
            case 2: CreateAppWindow(_strCalculator, _strCalculatorLen); break;
            case 3: CreateAppWindow(_strNotepad, _strNotepadLen); break;
            case 4: CreateAppWindow(_strTerminal, _strTerminalLen); break;
            case 5: CreateAppWindow(_strClock, _strClockLen); break;
        }
    }

    private static void CreateAppWindow(char* title, int titleLen)
    {
        Window.Create(100, 100, 260, 180, title, titleLen,
            WindowStyle.Visible | WindowStyle.HasClose | WindowStyle.HasMax | WindowStyle.HasMin);
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
}
