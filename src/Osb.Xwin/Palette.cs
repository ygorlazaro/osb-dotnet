// The 16 VGA palette indices, named for the roles the desktop gives them.
// Mirrors Osb.Kernel/Graphics/Palette.cs.

namespace Osb.Xwin;

internal static class Palette
{
    public const byte Black = 0;
    public const byte Blue = 1;
    public const byte Green = 2;
    public const byte Cyan = 3;
    public const byte Red = 4;
    public const byte Magenta = 5;
    public const byte Brown = 6;
    public const byte LightGray = 7;
    public const byte DarkGray = 8;
    public const byte LightBlue = 9;
    public const byte LightGreen = 10;
    public const byte LightCyan = 11;
    public const byte LightRed = 12;
    public const byte LightMagenta = 13;
    public const byte Yellow = 14;
    public const byte White = 15;

    public const byte Desktop = Cyan;
    public const byte WindowFace = LightGray;
    public const byte WindowShadow = DarkGray;
    public const byte TitleActive = Blue;
    public const byte TitleInactive = DarkGray;
    public const byte TitleText = White;
    public const byte MenuText = Black;
    public const byte Highlight = White;
    public const byte HighlightBg = Blue;
    public const byte IconText = Black;
}
