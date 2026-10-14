// GFX <mode> - switch the display into graphics mode and draw a test screen.
//
// This exists so the mode switch, the BIOS font and the drawing primitives
// can be checked on a real screen before anything is built on top of them.
// GFX TEXT returns to the 80x25 text console.

using Osb.Kernel.Graphics;
using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class GfxCommand
{
    private const string S_Upper = "ABCDEFGHIJKLM";
    private const string S_Lower = "abcdefghijklm";
    private const string S_Digits = "0123456789 +-*/";
    private const string S_FontOk = "font loaded from BIOS";
    private const string S_FontBad = "BIOS FONT MISSING";
    private const string S_Title = "OSB desktop";
    private const string S_Ok = "OK";

    public static void Run(char* line, int len)
    {
        int i = 3;                                    // past "GFX"
        while (i < len && line[i] == ' ') i++;

        bool wantText = StartsWith(line, len, i, 'T', 'E', 'X', 'T');
        if (wantText)
        {
            Gfx.Exit();
            Vga.Clear();
            Vga.WriteLine("Back in 80x25 text mode.");
            return;
        }

        Gfx.Enter();
        Font.Load();
        Test();
    }

    private static bool StartsWith(char* p, int len, int at, char a, char b, char c, char d)
    {
        if (at + 4 > len) return false;
        return p[at] == a && p[at + 1] == b && p[at + 2] == c && p[at + 3] == d;
    }

    // A screen that shows what is working: palette, fills, bevels, and the
    // font (or its absence, which is worth being able to see).
    private static void Test()
    {
        Gfx.Clear(Palette.Desktop);

        // Full palette strip along the top.
        for (int c = 0; c < 16; c++)
            Gfx.FillRect(c * 40, 0, 40, 30, (byte)c);

        // A beveled panel, the shape everything else will be built from.
        Gfx.FillRect(20, 50, 300, 140, Palette.WindowFace);
        Gfx.Bevel(20, 50, 300, 140, Palette.WindowFace, Palette.White, Palette.DarkGray);
        Gfx.FillRect(22, 52, 296, 20, Palette.TitleActive);
        Text(30, 54, S_Title, Palette.TitleText, Palette.TitleActive);

        // Font sample.
        Text(40, 90, S_Upper, Palette.IconText, Palette.WindowFace);
        Text(40, 110, S_Lower, Palette.IconText, Palette.WindowFace);
        Text(40, 130, S_Digits, Palette.IconText, Palette.WindowFace);
        if (Font.Loaded) Text(40, 160, S_FontOk, Palette.IconText, Palette.WindowFace);
        else Text(40, 160, S_FontBad, Palette.IconText, Palette.WindowFace);

        // Unaligned fill check: the run after the character ends.
        Gfx.FillRect(400, 50, 200, 60, Palette.Yellow);
        Gfx.FillRect(403, 53, 194, 54, Palette.LightGreen);
        Gfx.Bevel(400, 50, 200, 60, Palette.LightGreen, Palette.White, Palette.Black);

        // Rounded-looking button.
        Gfx.FillRect(400, 140, 90, 26, Palette.LightGray);
        Gfx.Bevel(400, 140, 90, 26, Palette.LightGray, Palette.White, Palette.DarkGray);
        Text(414, 145, S_Ok, Palette.IconText, Palette.LightGray);

        Gfx.Reassert();
        Marker();
        Report();
    }

    // Puts a marker in the text buffer and floods the aperture. Whichever one
    // appears on screen says which surface QEMU is actually rendering.
    private static void Marker()
    {
        byte* text = (byte*)0xB8000;
        fixed (char* s = "GFX-TEXT-MARKER")
        {
            for (int i = 0; i < 15; i++)
                text[i * 2] = (byte)s[i];
            text[15 * 2] = 0;
            text[15 * 2 + 1] = 0x0F;
        }
    }

    // Read the framebuffer back through the card. If these do not match what
    // was just drawn, the writes are not landing where the scanner looks,
    // which no amount of further drawing will fix.
    private static void Report()
    {
        // What does the card actually hold? QEMU decides text versus graphics
        // from GR06 bit 0, so getting that wrong leaves it rendering the text
        // console no matter what is in the framebuffer.
        Vga.Write("gfx: GR06=");
        Vga.WriteHex(Reg(0x3CE, 6));
        Vga.Write(" SR2=");
        Vga.WriteHex(Reg(0x3C4, 2));
        Vga.Write(" SR4=");
        Vga.WriteHex(Reg(0x3C4, 4));
        Vga.Write(" CRTC0C=");
        Vga.WriteHex(Reg(0x3D4, 0x0C));
        Vga.Write(" CRTC13=");
        Vga.WriteHex(Reg(0x3D4, 0x13));
        NativeMethods.outb(0x3DA, 0x00);
        NativeMethods.outb(0x3C0, 0x10);
        Vga.Write(" attr10=");
        Vga.WriteHex(NativeMethods.inb(0x3C1));
        Vga.PutChar('\n');

        Vga.Write("gfx: font=");
        Vga.WriteDec(Font.Loaded ? 1UL : 0UL);
        Vga.Write(" fb[0]=");
        Vga.WriteHex(((byte*)0xA0000)[0]);
        Vga.PutChar('\n');
    }

    private static byte Reg(ushort port, byte index)
    {
        NativeMethods.outb(port, index);
        return NativeMethods.inb((ushort)(port + 1));
    }

    // Draws a literal. The freestanding build has no ToCharArray and no
    // string concatenation, so callers pass fixed text.
    private static void Text(int x, int y, string s, byte fg, byte bg)
    {
        Font.DrawString(x, y, s, fg, bg);
    }
}