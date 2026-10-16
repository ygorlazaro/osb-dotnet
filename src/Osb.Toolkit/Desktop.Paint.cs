// OSTK - desktop painting: deskbar/taskbar, menus, shutdown dialog, icons.

using Osb.Xwin;

namespace Osb.Toolkit;

internal static unsafe partial class Desktop
{
    private const int DeskbarButtonX = 4;
    private const int DeskbarButtonW = DeskbarWidth - 8;

    private static int FirstWindowButtonY() => 6 + MenuButtonH + 6;
    private static int ShutdownButtonY() => Osb.Xwin.Gfx.Height - 30;

    private const int ShutdownDialogW = 220;
    private const int ShutdownDialogH = 110;
    private static int ShutdownDialogX() => (Osb.Xwin.Gfx.Width - ShutdownDialogW) / 2;
    private static int ShutdownDialogY() => (Osb.Xwin.Gfx.Height - ShutdownDialogH) / 2;

    private static int WindowButtonAt(int x, int y)
    {
        if (x < DeskbarButtonX || x >= DeskbarButtonX + DeskbarButtonW) return -1;
        int by = FirstWindowButtonY();
        int bh = PanelButtonH - 4;
        for (int i = 0; i < Osb.Xwin.WindowManager.WindowCount; i++)
        {
            // Minimized windows keep their button: it is the way to restore them.
            Osb.Xwin.WindowManager.GetWindowInfo(i, out _, out _, out bool vis, out _, out _);
            if (!vis) continue;
            if (y >= by && y < by + bh) return i;
            by += PanelButtonH + 4;
            if (by + bh > Osb.Xwin.Gfx.Height - 34) break;
        }
        return -1;
    }

    private static void DrawLeftPanel()
    {
        int w = DeskbarWidth;
        int h = Osb.Xwin.Gfx.Height;

        Graphics.FillRect(0, 0, w, h, Palette.WindowFace);
        Graphics.VLine(w, 0, h, Palette.DarkGray);

        int my = 6;
        byte mface = _menuOpen ? Palette.HighlightBg : Palette.WindowFace;
        Graphics.FillRect(DeskbarButtonX, my, DeskbarButtonW, MenuButtonH - 4, mface);
        Graphics.Bevel(DeskbarButtonX, my, DeskbarButtonW, MenuButtonH - 4,
            mface, Palette.White, Palette.DarkGray);
        Graphics.DrawString(DeskbarButtonX + 6, my + 5, _strOSB, _strOSBLen, Palette.Black, mface);

        int by = FirstWindowButtonY();
        int bh = PanelButtonH - 4;
        for (int i = 0; i < Osb.Xwin.WindowManager.WindowCount; i++)
        {
            Osb.Xwin.WindowManager.GetWindowInfo(i, out char* title, out int tlen,
                out bool vis, out bool min, out bool active);
            if (!vis) continue;
            byte face = active ? Palette.HighlightBg : Palette.WindowFace;
            // A minimized window's button is greyed; clicking it restores it.
            byte textColor = active ? Palette.White : (min ? Palette.DarkGray : Palette.Black);
            Graphics.FillRect(DeskbarButtonX, by, DeskbarButtonW, bh, face);
            Graphics.Bevel(DeskbarButtonX, by, DeskbarButtonW, bh, face,
                Palette.White, Palette.DarkGray);
            int showLen = tlen;
            int maxChars = (DeskbarButtonW - 6) / Osb.Xwin.Font.GlyphWidth;
            if (showLen > maxChars) showLen = maxChars;
            if (showLen > 0)
                Graphics.DrawString(DeskbarButtonX + 6, by + 4, title, showLen,
                    textColor, face);
            by += PanelButtonH + 4;
            if (by + bh > Osb.Xwin.Gfx.Height - 34) break;
        }

        int sy = ShutdownButtonY();
        Graphics.FillRect(DeskbarButtonX, sy, DeskbarButtonW, MenuButtonH - 4, Palette.LightRed);
        Graphics.Bevel(DeskbarButtonX, sy, DeskbarButtonW, MenuButtonH - 4,
            Palette.LightRed, Palette.White, Palette.DarkGray);
        Graphics.DrawString(DeskbarButtonX + 6, sy + 5, _strShutDown, _strShutDownLen,
            Palette.Black, Palette.LightRed);
    }

    private static void DrawAppMenu()
    {
        int mx = DeskbarWidth + 4;
        int my = 6 + MenuButtonH;
        const int itemH = 18;
        const int mw = 120;
        int mh = _appCount * itemH + 8;
        if (my + mh > Osb.Xwin.Gfx.Height - 4) my = Osb.Xwin.Gfx.Height - mh - 4;
        if (my < 4) my = 4;

        Graphics.FillRect(mx, my, mw, mh, Palette.WindowFace);
        Graphics.Rect(mx, my, mw, mh, Palette.DarkGray);
        Graphics.HLine(mx, my + mh - 1, mw, Palette.DarkGray);

        for (int i = 0; i < _appCount; i++)
        {
            int iy = my + 4 + i * itemH;
            char* name = AppName(_appIconIdx[i], out int nameLen);
            bool sel = i == _menuSelected;
            if (sel)
                Graphics.FillRect(mx + 2, iy, mw - 4, itemH - 2, Palette.HighlightBg);
            Graphics.DrawString(mx + 8, iy + 4, name, nameLen,
                sel ? Palette.Highlight : Palette.IconText,
                sel ? Palette.HighlightBg : Palette.WindowFace);
        }
    }

    private static void DrawShutdownDialog()
    {
        int dx = ShutdownDialogX();
        int dy = ShutdownDialogY();

        Graphics.FillRect(dx, dy, ShutdownDialogW, ShutdownDialogH, Palette.WindowFace);
        Graphics.Rect(dx, dy, ShutdownDialogW, ShutdownDialogH, Palette.DarkGray);
        Graphics.FillRect(dx, dy, ShutdownDialogW, 18, Palette.TitleActive);
        Graphics.DrawString(dx + 8, dy + 4, _strShutDownTitle, _strShutDownTitleLen,
            Palette.TitleText, Palette.TitleActive);

        string[] opts = { "Turn off", "Reboot", "Back to terminal", "Cancel" };
        int oy = dy + 30;
        for (int i = 0; i < 4; i++)
        {
            bool sel = i == _shutdownSelected;
            if (sel)
                Graphics.FillRect(dx + 10, oy, ShutdownDialogW - 20, 16, Palette.HighlightBg);
            char* txt = Str(opts[i], out int len);
            Graphics.DrawString(dx + 16, oy + 3, txt, len,
                sel ? Palette.Highlight : Palette.Black,
                sel ? Palette.HighlightBg : Palette.WindowFace);
            oy += 22;
        }
    }

    private static void DrawIcon32(int x, int y, byte type, byte bg)
    {
        Graphics.FillRect(x, y, 32, 32, Palette.WindowFace);
        Graphics.Bevel(x, y, 32, 32, Palette.WindowFace, Palette.White, Palette.DarkGray);
        Graphics.FillRect(x + 2, y + 2, 28, 8, Palette.TitleActive);

        if (type == 1)
        {
            Graphics.FillRect(x + 6, y + 14, 20, 13, Palette.Yellow);
            Graphics.Rect(x + 6, y + 14, 20, 13, Palette.Brown);
            Graphics.FillRect(x + 6, y + 12, 8, 3, Palette.Yellow);
            Graphics.Rect(x + 6, y + 12, 8, 3, Palette.Brown);
        }
        else
        {
            Graphics.FillRect(x + 8, y + 14, 16, 15, Palette.White);
            Graphics.Rect(x + 8, y + 14, 16, 15, Palette.DarkGray);
            Graphics.HLine(x + 10, y + 18, 12, Palette.Blue);
            Graphics.HLine(x + 10, y + 21, 12, Palette.DarkGray);
            Graphics.HLine(x + 10, y + 24, 9, Palette.DarkGray);
        }
    }

    private static void DrawIconLabel(Icon* ic)
    {
        int textW = ic->nameLen * Osb.Xwin.Font.GlyphWidth;
        int tx = ic->x + (IconWidth - textW) / 2;
        if (tx < 0) tx = 0;
        bool sel = _selectedIcon >= 0 && ic == &_icons[_selectedIcon];
        byte bg = sel ? Palette.HighlightBg : Palette.Desktop;
        byte fg = sel ? Palette.Highlight : Palette.IconText;
        Graphics.DrawString(tx, ic->y + IconHeight + 2, ic->name, ic->nameLen, fg, bg);
    }

    private static char* Str(string s, out int len)
    {
        len = s.Length;
        char* ptr = (char*)Osb.Xwin.Arena.Alloc((len + 1) * 2);
        fixed (char* src = s)
        {
            for (int i = 0; i <= len; i++) ptr[i] = src[i];
        }
        return ptr;
    }
}
