// Bochs/QEMU display device: a linear framebuffer with no VGA involved.
//
// The card presents as PCI 1234:1111 (QEMU's "std" VGA and the standalone
// bochs-display both use this id). BAR0 is plain video RAM and BAR2 holds the
// DISPI registers, through which the mode is set. This is the opposite
// approach to the legacy VGA path in LegacyVga.cs: no sequencer, no CRTC, no
// plane arithmetic - just a byte per pixel.
//
// The legacy path is kept because it is what real hardware needs; this is what
// the desktop uses under the emulator, because the baseline proved it is the
// path that actually works here.

using Osb.Kernel.Hardware;
using Osb.Kernel.Network;

namespace Osb.Kernel.Graphics;

internal static unsafe class Bochs
{
    private const ushort VendorQemu = 0x1234;
    private const ushort DeviceVga = 0x1111;

    // DISPI register indices, as this device numbers them. Note that index 5
    // is BANK, not the classic VBEPOSITION, and the virtual width that sets
    // the stride lives at 6.
    private const int RegId = 0x0;
    private const int RegXres = 0x1;
    private const int RegYres = 0x2;
    private const int RegBpp = 0x3;
    private const int RegEnable = 0x4;
    private const int RegVirtWidth = 0x6;

    private const ushort ExpectedId = 0xB0C5;   // B0C5 = version 5
    private const ushort IdFallback = 0xFF00;   // seen on some builds

    // ENABLE (0x01) plus LFB (0x40): scan out the linear framebuffer at BAR0
    // rather than the VGA memory window.
    private const uint EnableLfb = 0x41;

    private const int BytesPerPixel = 4;

    // The Bochs VBE register block is reachable two ways: the classic I/O pair
    // at 0x1CE/0x1CF, or memory-mapped in the card's BAR2. Which one a card
    // implements varies, so Init probes both and remembers the winner.
    private const ushort VbeIndex = 0x1CE;
    private const ushort VbeData = 0x1CF;
    private static bool _viaPorts;

    public static bool Present;
    public static bool Ready;

    // Result of the last mode set, for diagnostics.
    public static ushort ReadId;
    public static ulong FrameBase;
    public static ulong RegBase;

    private static int _bus, _slot, _func;
    private static int _width, _height;

    // The framebuffer pointer and geometry for the drawing front-end.
    public static byte* Frame;
    public static int Stride;

    public static int Width => _width;
    public static int Height => _height;

    // Finds the card, turns on memory decoding, and sets a 32bpp mode.
    public static bool Init(int width, int height)
    {
        _width = width; _height = height;

        if (!Pci.Find(VendorQemu, DeviceVga, out _bus, out _slot, out _func))
        {
            Present = false;
            return false;
        }
        Present = true;

        ulong bar0, bar2;
        if (!Pci.DecodeBar(_bus, _slot, _func, 0, out bar0)) return false;
        if (!Pci.DecodeBar(_bus, _slot, _func, 2, out bar2)) return false;
        FrameBase = bar0;
        RegBase = bar2;

        // Without memory decoding the register writes are dropped and the mode
        // never takes, which looks like a hardware fault rather than a missing
        // bit in the command register.
        Pci.EnableBusMaster(_bus, _slot, _func);

        // Reading the ID register back confirms the whole chain - device found,
        // BAR decoded, decoding enabled, right offset - before anything is
        // programmed on the assumption it worked. The register block is
        // reachable either through the classic I/O pair or BAR2, so probe both.
        ushort idPorts = PortRead(RegId);
        ushort idMmio = RegBase != 0 ? *(ushort*)RegBase : (ushort)0xFFFF;

        if (idPorts == ExpectedId || idPorts == IdFallback)
        {
            _viaPorts = true;
            ReadId = idPorts;
        }
        else if (idMmio == ExpectedId || idMmio == IdFallback)
        {
            _viaPorts = false;
            ReadId = idMmio;
        }
        else
        {
            Ready = false;
            return false;
        }

        // Disable while reprogramming, then set the mode and hand over the
        // framebuffer. 32bpp is stored little-endian as BGRX.
        RegWrite(RegEnable, 0);
        RegWrite(RegXres, (ushort)width);
        RegWrite(RegYres, (ushort)height);
        RegWrite(RegBpp, (uint)BytesPerPixel * 8);
        RegWrite(RegVirtWidth, (ushort)width);
        RegWrite(RegEnable, EnableLfb);

        Frame = (byte*)FrameBase;
        Stride = width * BytesPerPixel;
        Ready = true;
        return true;
    }

    // Hands the card back to legacy VGA (the text console the shell lives in).
    public static void Disable()
    {
        if (!Ready) return;
        RegWrite(RegEnable, 0);
        Ready = false;
    }

    // Reads a DISPI register through the classic I/O pair, regardless of which
    // interface Init selects.
    private static ushort PortRead(int index)
    {
        NativeMethods.outw(VbeIndex, (ushort)index);
        return NativeMethods.inw(VbeData);
    }

    private static ushort RegRead(int index)
    {
        if (_viaPorts)
        {
            NativeMethods.outw(VbeIndex, (ushort)index);
            return NativeMethods.inw(VbeData);
        }
        return *(ushort*)(RegBase + (ulong)index * 2);
    }

    private static void RegWrite(int index, uint v)
    {
        if (_viaPorts)
        {
            NativeMethods.outw(VbeIndex, (ushort)index);
            NativeMethods.outw(VbeData, (ushort)v);
            return;
        }
        *(ushort*)(RegBase + (ulong)index * 2) = (ushort)v;
    }

    // ---- pixel access ----
    //
    // 32 bits per pixel, stored little-endian as x8r8g8b8: a 32-bit write of
    // 0x00RRGGBB lands the bytes blue, green, red in memory.

    public static void SetPixel(int x, int y, byte c)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return;
        uint rgb = Rgb(c);
        *(uint*)(Frame + y * Stride + x * BytesPerPixel) = rgb;
    }

    public static byte GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height) return 0;
        uint v = *(uint*)(Frame + y * Stride + x * BytesPerPixel);
        return NearestIndex(v);
    }

    // Fills a run of whole pixels; the fast path for solid rectangles.
    public static void FillRun(int x, int y, int w, byte c)
    {
        if (w <= 0) return;
        uint rgb = Rgb(c);
        uint* p = (uint*)(Frame + y * Stride + x * BytesPerPixel);
        for (int i = 0; i < w; i++) p[i] = rgb;
    }

    // The 16 VGA indices expanded to RGB, so the desktop can keep using one
    // palette across both display backends.
    public static uint Rgb(byte c)
    {
        uint r, g, b;
        switch (c & 0x0F)
        {
            case 0: r = 0; g = 0; b = 0; break;             // black
            case 1: r = 0; g = 0; b = 170; break;          // blue
            case 2: r = 0; g = 170; b = 0; break;          // green
            case 3: r = 0; g = 170; b = 170; break;        // cyan
            case 4: r = 170; g = 0; b = 0; break;          // red
            case 5: r = 170; g = 0; b = 170; break;        // magenta
            case 6: r = 170; g = 85; b = 0; break;         // brown
            case 7: r = 170; g = 170; b = 170; break;      // light grey
            case 8: r = 85; g = 85; b = 85; break;         // dark grey
            case 9: r = 85; g = 85; b = 255; break;        // light blue
            case 10: r = 85; g = 255; b = 85; break;       // light green
            case 11: r = 85; g = 255; b = 255; break;      // light cyan
            case 12: r = 255; g = 85; b = 85; break;       // light red
            case 13: r = 255; g = 85; b = 255; break;      // light magenta
            case 14: r = 255; g = 255; b = 85; break;      // yellow
            default: r = 255; g = 255; b = 255; break;     // white
        }
        return (r << 16) | (g << 8) | b;
    }

    // Reads a pixel back as a palette index, by nearest match. Only used for
    // diagnostics and tests, so clarity beats speed.
    internal static byte NearestIndex(uint rgb)
    {
        int r = (int)((rgb >> 16) & 0xFF);
        int g = (int)((rgb >> 8) & 0xFF);
        int b = (int)(rgb & 0xFF);
        int best = 0;
        int bestD = 0x7FFFFFFF;
        for (int i = 0; i < 16; i++)
        {
            uint c = Rgb((byte)i);
            int dr = r - (int)((c >> 16) & 0xFF);
            int dg = g - (int)((c >> 8) & 0xFF);
            int db = b - (int)(c & 0xFF);
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; }
        }
        return (byte)best;
    }
}
