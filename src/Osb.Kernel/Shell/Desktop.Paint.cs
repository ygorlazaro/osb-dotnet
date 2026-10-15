// Desktop shell - painting the desktop, icons, windows and taskbar.
//
// Split out of Desktop.cs (400-line rule); shares the desktop's static state
// through the partial class.

using Osb.Kernel.Cpu;
using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell;

internal static unsafe partial class Desktop
{
    private static void PaintDesktop()
    {
        // Clear desktop
        Gfx.Clear(Palette.Desktop);

        // Draw desktop icons
        DrawIcons();

        // Draw window frames
        WindowManager.PaintFrames();

        // Draw window client areas
        PaintWindowClients();

        // Draw taskbar
        DrawTaskbar();
    }

    private static void PaintWindowClients()
    {
        // Simpler: just paint all windows' client areas
        for (int i = 0; i < WindowManager.WindowCount; i++)
        {
            PaintWindowClient(i);
        }
    }

    private static void PaintWindowClient(int index)
    {
        int kind = WindowManager.GetKind(index);
        WindowManager.GetClientArea(index, out int cx, out int cy, out int cw, out int ch);

        if (cw <= 0 || ch <= 0) return;

        switch (kind)
        {
            case WindowManager.WkProgman:
                PaintProgmanClient(cx, cy, cw, ch);
                break;
            case WindowManager.WkApplication:
                PaintAppClient(cx, cy, cw, ch);
                break;
            case WindowManager.WkGroup:
                PaintGroupClient(cx, cy, cw, ch);
                break;
        }
    }

    private static void PaintProgmanClient(int cx, int cy, int cw, int ch)
    {
        // Draw group list on left
        Gfx.FillRect(cx, cy, 120, ch, Palette.WindowFace);
        Gfx.Bevel(cx, cy, 120, ch, Palette.WindowFace, Palette.White, Palette.DarkGray);

        Font.DrawString(cx + 8, cy + 8, _strMain, _strMainLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(cx + 8, cy + 24, _strAccessories, _strAccessoriesLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(cx + 8, cy + 40, _strGames, _strGamesLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(cx + 8, cy + 56, _strStartup, _strStartupLen, Palette.IconText, Palette.WindowFace);

        // Draw program items on right
        int px = cx + 130;
        int py = cy + 8;

        Font.DrawString(px, py, _strWrite, _strWriteLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(px, py + 16, _strPaintbrush, _strPaintbrushLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(px, py + 32, _strCalculator, _strCalculatorLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(px, py + 48, _strNotepad, _strNotepadLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(px, py + 64, _strTerminal, _strTerminalLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(px, py + 80, _strClock, _strClockLen, Palette.IconText, Palette.WindowFace);

        // Way back to the shell (closing this window exits the desktop).
        Font.DrawString(cx + 8, cy + ch - 14, _strCloseMsg, _strCloseMsgLen, Palette.IconText, Palette.WindowFace);
    }

    private static void PaintAppClient(int cx, int cy, int cw, int ch)
    {
        Font.DrawString(cx + 10, cy + 10, _strAppWindow, _strAppWindowLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(cx + 10, cy + 30, _strCloseMsg, _strCloseMsgLen, Palette.IconText, Palette.WindowFace);
    }

    private static void PaintGroupClient(int cx, int cy, int cw, int ch)
    {
        Font.DrawString(cx + 10, cy + 10, _strGroupWindow, _strGroupWindowLen, Palette.IconText, Palette.WindowFace);
        Font.DrawString(cx + 10, cy + 30, _strGroupMsg, _strGroupMsgLen, Palette.IconText, Palette.WindowFace);
    }

    private static void DrawIcons()
    {
        for (int i = 0; i < _iconCount; i++)
        {
            Icon* ic = &_icons[i];

            // Draw icon image (simplified - just a colored square)
            byte iconColor = ic->iconType == 1 ? Palette.LightBlue : Palette.LightGreen;
            Gfx.FillRect(ic->x + 16, ic->y, 32, 32, iconColor);
            Gfx.Bevel(ic->x + 16, ic->y, 32, 32, iconColor, Palette.White, Palette.DarkGray);

            // Draw icon label
            if (ic->name != null && ic->nameLen > 0)
            {
                int textW = ic->nameLen * Font.GlyphWidth;
                int tx = ic->x + (IconWidth - textW) / 2;
                if (tx < ic->x) tx = ic->x;
                Font.DrawString(tx, ic->y + IconHeight + 2, ic->name, ic->nameLen, Palette.IconText, Palette.Desktop);
            }
        }
    }

    private static void DrawTaskbar()
    {
        int ty = Gfx.Height - TaskbarHeight;

        // Taskbar background
        Gfx.FillRect(0, ty, Gfx.Width, TaskbarHeight, Palette.WindowFace);
        Gfx.HLine(0, ty, Gfx.Width, Palette.White);  // Top highlight

        // Start button
        Gfx.FillRect(_taskbarStartX, ty + 2, _startButtonW, TaskbarHeight - 4, Palette.TitleActive);
        Gfx.Bevel(_taskbarStartX, ty + 2, _startButtonW, TaskbarHeight - 4, Palette.TitleActive, Palette.White, Palette.DarkGray);
        Font.DrawString(_taskbarStartX + 8, ty + 5, _strStart, _strStartLen, Palette.TitleText, Palette.TitleActive);

        // Clock on right
        // Get time from RTC
        int hours = Rtc.Hour;
        int minutes = Rtc.Minute;
        int seconds = Rtc.Second;

        char* timeBuf = stackalloc char[9];
        timeBuf[0] = (char)('0' + hours / 10);
        timeBuf[1] = (char)('0' + hours % 10);
        timeBuf[2] = ':';
        timeBuf[3] = (char)('0' + minutes / 10);
        timeBuf[4] = (char)('0' + minutes % 10);
        timeBuf[5] = ':';
        timeBuf[6] = (char)('0' + seconds / 10);
        timeBuf[7] = (char)('0' + seconds % 10);
        timeBuf[8] = (char)0;

        int tw = 8 * Font.GlyphWidth;
        Font.DrawString(Gfx.Width - tw - 8, ty + 4, timeBuf, 8, Palette.IconText, Palette.WindowFace);
    }
}
