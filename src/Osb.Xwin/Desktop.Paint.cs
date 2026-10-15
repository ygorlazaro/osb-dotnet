// XWIN desktop - painting the desktop, icons, windows and taskbar.
//
// Split out of Desktop.cs (400-line rule); shares the desktop's static state
// through the partial class. Ported from the kernel's Shell/Desktop.Paint.cs.

namespace Osb.Xwin;

internal static unsafe partial class Desktop
{
    private static void PaintDesktop()
    {
        Gfx.Clear(Palette.Desktop);
        DrawIcons();
        WindowManager.PaintFrames();
        PaintWindowClients();
        DrawTaskbar();
    }

    private static void PaintWindowClients()
    {
        for (int i = 0; i < WindowManager.WindowCount; i++)
            PaintWindowClient(i);
    }

    private static void PaintWindowClient(int index)
    {
        WindowManager.GetClientArea(index, out int cx, out int cy, out int cw, out int ch);
        if (cw <= 0 || ch <= 0) return;

        int kind = WindowManager.GetKind(index);

        if (kind == WindowManager.WkProgman) PaintProgmanClient(cx, cy, cw, ch);
        else if (kind == WindowManager.WkGroup) PaintGroupClient(cx, cy, cw, ch);
        else PaintAppClient(cx, cy, cw, ch);
    }

    // Program Manager: a menu bar over a grid of program items.
    private static void PaintProgmanClient(int cx, int cy, int cw, int ch)
    {
        DrawMenuBar(cx, cy, cw);

        int mx = cx + 8;
        mx = Font.DrawString(mx, cy + 4, _strFile, _strFileLen, Palette.MenuText, Palette.WindowFace) + 14;
        mx = Font.DrawString(mx, cy + 4, _strOptions, _strOptionsLen, Palette.MenuText, Palette.WindowFace) + 14;
        mx = Font.DrawString(mx, cy + 4, _strWindow, _strWindowLen, Palette.MenuText, Palette.WindowFace) + 14;
        Font.DrawString(mx, cy + 4, _strHelp, _strHelpLen, Palette.MenuText, Palette.WindowFace);

        int itemY = cy + 26;
        int itemX = cx + 12;
        DrawItem(itemX, itemY, (byte)0, _strWrite, _strWriteLen);
        DrawItem(itemX + 150, itemY, (byte)1, _strPaintbrush, _strPaintbrushLen);
        DrawItem(itemX, itemY + 44, (byte)2, _strCalculator, _strCalculatorLen);
        DrawItem(itemX + 150, itemY + 44, (byte)3, _strNotepad, _strNotepadLen);
        DrawItem(itemX, itemY + 88, (byte)4, _strTerminal, _strTerminalLen);
        DrawItem(itemX + 150, itemY + 88, (byte)5, _strClock, _strClockLen);

        // A hint line at the bottom, like the Program Manager's group window.
        int lineY = cy + ch - 16;
        if (lineY > itemY + 130)
            Font.DrawString(cx + 12, lineY, _strCloseMsg, _strCloseMsgLen, Palette.DarkGray, Palette.WindowFace);
    }

    private static void PaintGroupClient(int cx, int cy, int cw, int ch)
    {
        DrawMenuBar(cx, cy, cw);
        int itemY = cy + 26;
        DrawItem(cx + 12, itemY, (byte)0, _strWrite, _strWriteLen);
        DrawItem(cx + 12, itemY + 44, (byte)1, _strPaintbrush, _strPaintbrushLen);
        DrawItem(cx + 12, itemY + 88, (byte)2, _strNotepad, _strNotepadLen);
        Font.DrawString(cx + 12, cy + ch - 16, _strGroupMsg, _strGroupMsgLen, Palette.DarkGray, Palette.WindowFace);
    }

    private static void PaintAppClient(int cx, int cy, int cw, int ch)
    {
        // Centred caption and a close hint, like a plain application window.
        int tx = cx + (cw - _strAppWindowLen * Font.GlyphWidth) / 2;
        if (tx < cx + 4) tx = cx + 4;
        Font.DrawString(tx, cy + ch / 2 - 12, _strAppWindow, _strAppWindowLen, Palette.Black, Palette.WindowFace);

        int hx = cx + (cw - _strCloseMsgLen * Font.GlyphWidth) / 2;
        if (hx < cx + 4) hx = cx + 4;
        Font.DrawString(hx, cy + ch / 2 + 4, _strCloseMsg, _strCloseMsgLen, Palette.DarkGray, Palette.WindowFace);
    }

    private static void DrawMenuBar(int cx, int cy, int cw)
    {
        Gfx.FillRect(cx, cy, cw, 14, Palette.WindowFace);
        Gfx.HLine(cx, cy + 13, cw, Palette.DarkGray);
    }

    // A program item: a 16x16 glyph with its label to the right.
    private static void DrawItem(int x, int y, byte glyph, char* name, int nameLen)
    {
        DrawIcon16(x, y, glyph);
        Font.DrawString(x + 22, y + 4, name, nameLen, Palette.Black, Palette.WindowFace);
    }

    // ---- desktop icons ----

    private static void DrawIcons()
    {
        for (int i = 0; i < _iconCount; i++)
        {
            Icon* ic = &_icons[i];
            bool sel = i == _selectedIcon;

            if (sel)
                Gfx.FillRect(ic->x - 2, ic->y - 2, IconWidth + 4, IconHeight + 22, Palette.HighlightBg);

            byte bg = sel ? Palette.HighlightBg : Palette.Desktop;
            byte fg = sel ? Palette.Highlight : Palette.IconText;

            DrawIcon32(ic->x + 16, ic->y + 8, ic->iconType, bg);

            int textW = ic->nameLen * Font.GlyphWidth;
            int tx = ic->x + (IconWidth - textW) / 2;
            if (tx < 0) tx = 0;
            Font.DrawString(tx, ic->y + IconHeight + 2, ic->name, ic->nameLen, fg, bg);
        }
    }

    private static void DrawIcon32(int x, int y, byte type, byte bg)
    {
        // A framed "window" glyph: blue title bar with a white title, then a
        // small mark that hints at the kind (folder for a group, page for an
        // application).
        Gfx.FillRect(x, y, 32, 32, Palette.WindowFace);
        Gfx.Bevel(x, y, 32, 32, Palette.WindowFace, Palette.White, Palette.DarkGray);
        Gfx.FillRect(x + 2, y + 2, 28, 8, Palette.TitleActive);

        if (type == 1)
        {
            // Group: a folder with a tab.
            Gfx.FillRect(x + 6, y + 14, 20, 13, Palette.Yellow);
            Gfx.Rect(x + 6, y + 14, 20, 13, Palette.Brown);
            Gfx.FillRect(x + 6, y + 12, 8, 3, Palette.Yellow);
            Gfx.Rect(x + 6, y + 12, 8, 3, Palette.Brown);
        }
        else
        {
            // Application: a page with lines of text.
            Gfx.FillRect(x + 8, y + 14, 16, 15, Palette.White);
            Gfx.Rect(x + 8, y + 14, 16, 15, Palette.DarkGray);
            Gfx.HLine(x + 10, y + 18, 12, Palette.Blue);
            Gfx.HLine(x + 10, y + 21, 12, Palette.DarkGray);
            Gfx.HLine(x + 10, y + 24, 9, Palette.DarkGray);
        }
    }

    private static void DrawIcon16(int x, int y, byte type)
    {
        Gfx.FillRect(x, y, 16, 16, Palette.WindowFace);
        Gfx.Bevel(x, y, 16, 16, Palette.WindowFace, Palette.White, Palette.DarkGray);
        Gfx.FillRect(x + 1, y + 1, 14, 4, Palette.TitleActive);
        Gfx.FillRect(x + 3, y + 7, 10, 7, type == 0 ? Palette.White : Palette.Yellow);
        Gfx.Rect(x + 3, y + 7, 10, 7, Palette.DarkGray);
    }

    // Build a stack string on demand (used for the few strings that are not
    // held in the desktop's string table).
    private static char* Str(string s, out int len)
    {
        len = s.Length;
        char* ptr = (char*)Arena.Alloc((len + 1) * 2);
        fixed (char* src = s)
        {
            for (int i = 0; i <= len; i++) ptr[i] = src[i];
        }
        return ptr;
    }

    // ---- taskbar ----

    private static void DrawTaskbar()
    {
        int ty = Gfx.Height - TaskbarHeight;

        Gfx.FillRect(0, ty, Gfx.Width, TaskbarHeight, Palette.WindowFace);
        Gfx.HLine(0, ty, Gfx.Width, Palette.White);

        Gfx.FillRect(_taskbarStartX, ty + 2, _startButtonW, TaskbarHeight - 4, Palette.WindowFace);
        Gfx.Bevel(_taskbarStartX, ty + 2, _startButtonW, TaskbarHeight - 4,
                  Palette.WindowFace, Palette.White, Palette.DarkGray);
        Font.DrawString(_taskbarStartX + 10, ty + 6, _strStart, _strStartLen, Palette.Black, Palette.WindowFace);

        // Clock: "HH:MM:SS" at the right edge.
        char* buf = stackalloc char[9];
        int h = Rtc.Hour, m = Rtc.Minute, s = Rtc.Second;
        buf[0] = (char)('0' + (h / 10) % 10);
        buf[1] = (char)('0' + h % 10);
        buf[2] = ':';
        buf[3] = (char)('0' + (m / 10) % 10);
        buf[4] = (char)('0' + m % 10);
        buf[5] = ':';
        buf[6] = (char)('0' + (s / 10) % 10);
        buf[7] = (char)('0' + s % 10);
        buf[8] = (char)0;

        Gfx.FillRect(Gfx.Width - 74, ty + 2, 70, TaskbarHeight - 4, Palette.WindowFace);
        Gfx.Bevel(Gfx.Width - 74, ty + 2, 70, TaskbarHeight - 4,
                  Palette.WindowFace, Palette.DarkGray, Palette.White);
        Font.DrawString(Gfx.Width - 68, ty + 6, buf, 8, Palette.Black, Palette.WindowFace);
    }
}
