// XWIN desktop - painting the desktop, icons, windows and deskbar.
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
        DrawLeftPanel();

        if (_shutdownOpen) DrawShutdownDialog();
        else if (_menuOpen) DrawAppMenu();
    }

    // ---- deskbar geometry ----

    private const int DeskbarButtonX = 4;
    private const int DeskbarButtonW = DeskbarWidth - 8;

    private static int FirstWindowButtonY() => 6 + MenuButtonH + 6;
    private static int ShutdownButtonY() => Gfx.Height - 30;

    private const int ShutdownDialogW = 220;
    private const int ShutdownDialogH = 110;
    private static int ShutdownDialogX() => (Gfx.Width - ShutdownDialogW) / 2;
    private static int ShutdownDialogY() => (Gfx.Height - ShutdownDialogH) / 2;

    // Which stacked window button (if any) is at (x, y) in the deskbar, or -1.
    private static int WindowButtonAt(int x, int y)
    {
        if (x < DeskbarButtonX || x >= DeskbarButtonX + DeskbarButtonW) return -1;
        int by = FirstWindowButtonY();
        int bh = PanelButtonH - 4;
        for (int i = 0; i < WindowManager.WindowCount; i++)
        {
            WindowManager.GetWindowInfo(i, out _, out _, out bool vis, out bool min, out _);
            if (!vis || min) continue;
            if (y >= by && y < by + bh) return i;
            by += PanelButtonH + 4;
            if (by + bh > Gfx.Height - 34) break;
        }
        return -1;
    }

    private static bool InDeskbarButton(int x, int y)
    {
        return x >= DeskbarButtonX && x < DeskbarButtonX + DeskbarButtonW;
    }

    // ---- deskbar ----

    private static void DrawLeftPanel()
    {
        int w = DeskbarWidth;
        int h = Gfx.Height;

        Gfx.FillRect(0, 0, w, h, Palette.WindowFace);
        Gfx.VLine(w, 0, h, Palette.DarkGray);

        // Menu button (top): the first button, opens the installed-apps menu.
        int my = 6;
        byte mface = _menuOpen ? Palette.HighlightBg : Palette.WindowFace;
        Gfx.FillRect(DeskbarButtonX, my, DeskbarButtonW, MenuButtonH - 4, mface);
        Gfx.Bevel(DeskbarButtonX, my, DeskbarButtonW, MenuButtonH - 4,
                  mface, Palette.White, Palette.DarkGray);
        Font.DrawString(DeskbarButtonX + 6, my + 5, _strOSB, _strOSBLen, Palette.Black, mface);

        // Stacked window buttons below the menu button.
        int by = FirstWindowButtonY();
        int bh = PanelButtonH - 4;
        for (int i = 0; i < WindowManager.WindowCount; i++)
        {
            WindowManager.GetWindowInfo(i, out char* title, out int tlen,
                                         out bool vis, out bool min, out bool active);
            if (!vis || min) continue;
            byte face = active ? Palette.HighlightBg : Palette.WindowFace;
            Gfx.FillRect(DeskbarButtonX, by, DeskbarButtonW, bh, face);
            Gfx.Bevel(DeskbarButtonX, by, DeskbarButtonW, bh, face,
                      Palette.White, Palette.DarkGray);
            int showLen = tlen;
            int maxChars = (DeskbarButtonW - 6) / Font.GlyphWidth;
            if (showLen > maxChars) showLen = maxChars;
            if (showLen > 0)
                Font.DrawString(DeskbarButtonX + 6, by + 4, title, showLen,
                                active ? Palette.White : Palette.Black, face);
            by += PanelButtonH + 4;
            if (by + bh > Gfx.Height - 34) break;
        }

        // Shutdown button at the bottom end.
        int sy = ShutdownButtonY();
        Gfx.FillRect(DeskbarButtonX, sy, DeskbarButtonW, MenuButtonH - 4, Palette.LightRed);
        Gfx.Bevel(DeskbarButtonX, sy, DeskbarButtonW, MenuButtonH - 4,
                  Palette.LightRed, Palette.White, Palette.DarkGray);
        Font.DrawString(DeskbarButtonX + 6, sy + 5, _strShutDown, _strShutDownLen,
                        Palette.Black, Palette.LightRed);
    }

    // Installed-apps popup, shown below the deskbar's first button.
    private static void DrawAppMenu()
    {
        int mx = DeskbarWidth + 4;
        int my = 6 + MenuButtonH;
        const int itemH = 18;
        const int mw = 120;
        int mh = _appCount * itemH + 8;
        if (my + mh > Gfx.Height - 4) my = Gfx.Height - mh - 4;
        if (my < 4) my = 4;

        Gfx.FillRect(mx, my, mw, mh, Palette.WindowFace);
        Gfx.Rect(mx, my, mw, mh, Palette.DarkGray);
        Gfx.HLine(mx, my + mh - 1, mw, Palette.DarkGray);

        for (int i = 0; i < _appCount; i++)
        {
            int iy = my + 4 + i * itemH;
            int ico = _appIconIdx[i];
            bool sel = i == _menuSelected;
            if (sel)
                Gfx.FillRect(mx + 2, iy, mw - 4, itemH - 2, Palette.HighlightBg);
            Font.DrawString(mx + 8, iy + 4, _icons[ico].name, _icons[ico].nameLen,
                            sel ? Palette.Highlight : Palette.IconText,
                            sel ? Palette.HighlightBg : Palette.WindowFace);
        }
    }

    // Turn off / reboot / back-to-terminal confirmation dialog.
    private static void DrawShutdownDialog()
    {
        int dx = ShutdownDialogX();
        int dy = ShutdownDialogY();

        Gfx.FillRect(dx, dy, ShutdownDialogW, ShutdownDialogH, Palette.WindowFace);
        Gfx.Rect(dx, dy, ShutdownDialogW, ShutdownDialogH, Palette.DarkGray);
        Gfx.FillRect(dx, dy, ShutdownDialogW, 18, Palette.TitleActive);
        Font.DrawString(dx + 8, dy + 4, _strShutDownTitle, _strShutDownTitleLen,
                        Palette.TitleText, Palette.TitleActive);

        string[] opts = { "Turn off", "Reboot", "Back to terminal", "Cancel" };
        int oy = dy + 30;
        for (int i = 0; i < 4; i++)
        {
            bool sel = i == _shutdownSelected;
            if (sel)
                Gfx.FillRect(dx + 10, oy, ShutdownDialogW - 20, 16, Palette.HighlightBg);
            char* txt = Str(opts[i], out int len);
            Font.DrawString(dx + 16, oy + 3, txt, len,
                            sel ? Palette.Highlight : Palette.Black,
                            sel ? Palette.HighlightBg : Palette.WindowFace);
            oy += 22;
        }
    }

    // ---- window clients ----

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
            Font.DrawString(tx, ic->y + IconHeight + 2, (char*)Arena.Alloc(1), 0, fg, bg);
            // keep the pointer alive by drawing a literal manually via a helper below
            {
                char* _n = (char*)Arena.Alloc(6);
                _n[0] = 'M'; _n[1] = 'a'; _n[2] = 'i'; _n[3] = 'n'; _n[4] = (char)0;
                Font.DrawString(tx, ic->y + IconHeight + 2, _n, 4, fg, bg);
            }
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
}
