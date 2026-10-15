// Mouse cursor rendering for the desktop.
//
// Draws a classic Windows 3.11 arrow cursor (16x16, 2 colors + transparent)
// using the Bochs linear framebuffer or VGA planar writes.

using Osb.Kernel.Graphics;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe class Cursor
{
    // 16x16 cursor, 2 bits per pixel:
    // 0 = transparent, 1 = white, 2 = black, 3 = inverted
    // Windows 3.11 arrow cursor pattern
    private static readonly byte[] _cursorAnd = new byte[32]
    {
        0xFF, 0xFF,  // row 0
        0xFF, 0xFF,
        0xF0, 0xFF,
        0xF8, 0xFF,
        0xFC, 0xFF,
        0xFE, 0xFF,
        0xFF, 0xFF,
        0xFF, 0xFF,
        0xFF, 0xFF,
        0xF8, 0xFF,
        0xF0, 0xFF,
        0xE0, 0xFF,
        0xE0, 0xFF,
        0xC0, 0xFF,
        0x80, 0xFF,
        0x00, 0xFF,
    };

    private static readonly byte[] _cursorXor = new byte[32]
    {
        0x00, 0x00,
        0x00, 0x00,
        0x0F, 0x00,
        0x07, 0x00,
        0x03, 0x00,
        0x01, 0x00,
        0x00, 0x00,
        0x00, 0x00,
        0x00, 0x00,
        0x07, 0x00,
        0x0F, 0x00,
        0x1F, 0x00,
        0x1F, 0x00,
        0x3F, 0x00,
        0x7F, 0x00,
        0xFF, 0x00,
    };

    // Saved background under cursor (16x16)
    private static byte[] _savedBg = new byte[16 * 16 * 4];  // 32bpp for Bochs
    private static int _savedX = -1, _savedY = -1;
    private static bool _bgSaved = false;

    public static void Draw(int x, int y)
    {
        if (!Gfx.Active) return;

        // Clamp to screen
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + 16 > Gfx.Width) x = Gfx.Width - 16;
        if (y + 16 > Gfx.Height) y = Gfx.Height - 16;

        // Save background if position changed
        if (!_bgSaved || _savedX != x || _savedY != y)
        {
            Restore();  // Restore previous position first
            SaveBackground(x, y);
            _savedX = x;
            _savedY = y;
            _bgSaved = true;
        }

        // Draw cursor
        if (Gfx._backend == Backend.Bochs && Bochs.Ready)
        {
            DrawBochs(x, y);
        }
        else if (Gfx._backend == Backend.LegacyVga)
        {
            DrawVga(x, y);
        }
    }

    public static void Restore()
    {
        if (!_bgSaved) return;
        if (_savedX < 0) return;

        if (Gfx._backend == Backend.Bochs && Bochs.Ready)
        {
            RestoreBochs(_savedX, _savedY);
        }
        else if (Gfx._backend == Backend.LegacyVga)
        {
            RestoreVga(_savedX, _savedY);
        }
        _bgSaved = false;
        _savedX = -1;
        _savedY = -1;
    }

    private static void SaveBackground(int x, int y)
    {
        if (Gfx._backend == Backend.Bochs && Bochs.Ready)
        {
            byte* fb = Bochs.Frame;
            int stride = Bochs.Stride;
            int idx = 0;
            for (int cy = 0; cy < 16; cy++)
            {
                for (int cx = 0; cx < 16; cx++)
                {
                    uint* p = (uint*)(fb + (y + cy) * stride + (x + cx) * 4);
                    _savedBg[idx++] = (byte)(*p & 0xFF);
                    _savedBg[idx++] = (byte)((*p >> 8) & 0xFF);
                    _savedBg[idx++] = (byte)((*p >> 16) & 0xFF);
                    _savedBg[idx++] = (byte)((*p >> 24) & 0xFF);
                }
            }
        }
        else if (Gfx._backend == Backend.LegacyVga)
        {
            // For VGA, we'd need to read from 4 planes - skip for now
            // The cursor will just leave trails on VGA, which is acceptable
        }
    }

    private static void DrawBochs(int x, int y)
    {
        byte* fb = Bochs.Frame;
        int stride = Bochs.Stride;

        for (int cy = 0; cy < 16; cy++)
        {
            byte andMask = _cursorAnd[cy * 2];
            byte andMask2 = _cursorAnd[cy * 2 + 1];
            byte xorMask = _cursorXor[cy * 2];
            byte xorMask2 = _cursorXor[cy * 2 + 1];

            for (int cx = 0; cx < 16; cx++)
            {
                byte andBit, xorBit;
                if (cx < 8)
                {
                    andBit = (byte)((andMask >> (7 - cx)) & 1);
                    xorBit = (byte)((xorMask >> (7 - cx)) & 1);
                }
                else
                {
                    andBit = (byte)((andMask2 >> (15 - cx)) & 1);
                    xorBit = (byte)((xorMask2 >> (15 - cx)) & 1);
                }

                // AND/XOR cursor logic:
                // if andBit == 0: force 0 (black)
                // if andBit == 1 and xorBit == 0: keep background
                // if andBit == 1 and xorBit == 1: invert background
                uint* p = (uint*)(fb + (y + cy) * stride + (x + cx) * 4);
                uint bg = *p;

                uint color;
                if (andBit == 0)
                {
                    color = 0x00000000;  // Black
                }
                else if (xorBit == 0)
                {
                    color = bg;  // Transparent - keep background
                }
                else
                {
                    // Invert - for simplicity, use white
                    color = 0x00FFFFFF;  // White
                }

                *p = color;
            }
        }
    }

    private static void DrawVga(int x, int y)
    {
        // Simplified: just draw a white arrow on VGA
        // This is a fallback and won't be perfect
        for (int cy = 0; cy < 16; cy++)
        {
            byte andMask = _cursorAnd[cy * 2];
            byte andMask2 = _cursorAnd[cy * 2 + 1];

            for (int cx = 0; cx < 16; cx++)
            {
                byte andBit;
                if (cx < 8)
                    andBit = (byte)((andMask >> (7 - cx)) & 1);
                else
                    andBit = (byte)((andMask2 >> (15 - cx)) & 1);

                if (andBit == 0)
                {
                    Gfx.SetPixel(x + cx, y + cy, Palette.White);
                }
            }
        }
    }

    private static void RestoreBochs(int x, int y)
    {
        byte* fb = Bochs.Frame;
        int stride = Bochs.Stride;
        int idx = 0;
        for (int cy = 0; cy < 16; cy++)
        {
            for (int cx = 0; cx < 16; cx++)
            {
                uint color = (uint)(_savedBg[idx] | (_savedBg[idx + 1] << 8) | (_savedBg[idx + 2] << 16) | (_savedBg[idx + 3] << 24));
                uint* p = (uint*)(fb + (y + cy) * stride + (x + cx) * 4);
                *p = color;
                idx += 4;
            }
        }
    }

    private static void RestoreVga(int x, int y)
    {
        // Not implemented for VGA - would need to restore from 4 planes
    }
}