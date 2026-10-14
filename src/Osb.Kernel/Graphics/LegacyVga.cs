// Legacy VGA path: four bit-planar framebuffer at A0000 (identity-mapped).
// Modeled on Mode 12h (640x480, 16 colours). Works on real hardware but
// does not scan out under the QEMU emulator; Bochs is preferred there.
//
// Sequencer / CRT controller / graphics controller / attribute ports.
// All ports are the classic IBM VGA port addresses.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

// Internal accessibility is intentional: Gfx.cs (same assembly, same namespace)
// needs to call these methods and access these members.
internal static unsafe class LegacyVga
{
    // Sequencer / CRT controller / graphics controller / attribute ports.
    private const ushort SeqIndex = 0x3C4;
    private const ushort SeqData = 0x3C5;
    private const ushort CrtcIndex = 0x3D4;
    private const ushort CrtcData = 0x3D5;
    private const ushort GfxIndex = 0x3CE;
    private const ushort GfxData = 0x3CF;
    private const ushort AttrIndex = 0x3C0;
    private const ushort AttrData = 0x3C1;
    private const ushort Misc = 0x3C2;
    private const ushort StatusA = 0x3DA;

    // Selects which bit planes subsequent writes reach.
    private const byte SeqMapMask = 0x0E;   // write plane enable

    // Bit index of the leftmost pixel inside a byte.
    private const int HighBit = 7;

    // The framebuffer aperture: the VGA card maps the linear address 0xA0000
    // to the segment A000:0000.  The kernel maps it via the identity mapping,
    // so the pointer is just that address cast.
    // Access via Layout.* as the original code does.

    // ---- mode control ----

    // Forces the card back to plain legacy VGA.
    //
    // While VBE is enabled the model reads a SEPARATE copy of the sequencer
    // registers, so every legacy write is quietly ignored: the mode appears to
    // take effect (the card reports a new resolution) while the framebuffer is
    // never scanned out. Turning VBE off first makes the register writes below
    // mean what they say.
    private static void DisableVbe()
    {
        NativeMethods.outb(0x1CE, 0x04);
        NativeMethods.outb(0x1CF, 0x01);
        NativeMethods.outb(0x1CF, 0x00);        // ENABLE = 0
    }

    // Program the card into Mode 12h (640x480, 16 colours).
    private static void ProgramMode12()
    {
        DisableVbe();

        // Sequencer: clocks, all four planes writable, character generator,
        // memory mode.
        NativeMethods.outb(SeqIndex, 0x01);
        NativeMethods.outb(SeqData, 0x01);
        NativeMethods.outb(SeqData, 0x0F);
        NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqData, 0x0E);
        NativeMethods.outb(SeqData, 0x0D);

        // Graphics controller. GR06 selects the graphics display and the
        // memory map; the usual 0x0E would point the card at the B8000 text
        // window, where writes to the framebuffer are simply dropped.
        NativeMethods.outb(GfxIndex, 0x00);
        NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxData, 0x05);
        NativeMethods.outb(GfxData, 0x0F);
        NativeMethods.outb(GfxData, 0xFF);

        // Attribute controller: sixteen palette entries, then mode control.
        NativeMethods.outb(StatusA, 0x00);
        for (byte i = 0; i < 16; i++) { NativeMethods.outb(AttrIndex, i); NativeMethods.outb(AttrData, i); }
        NativeMethods.outb(AttrIndex, 0x10); NativeMethods.outb(AttrData, 0x01);
        NativeMethods.outb(AttrIndex, 0x11); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x12); NativeMethods.outb(AttrData, 0x0F);
        NativeMethods.outb(AttrIndex, 0x13); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x14); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x15); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x16); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x17); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x18); NativeMethods.outb(AttrData, 0x0E);
        NativeMethods.outb(AttrIndex, 0x19); NativeMethods.outb(AttrData, 0x04);

        NativeMethods.outb(Misc, 0xE3);

        for (int i = 0; i < 24; i++)
        {
            NativeMethods.outb(CrtcIndex, (byte)i);
            NativeMethods.outb(CrtcData, _mode12.crtc[i]);
        }

        NativeMethods.outb(StatusA, 0x03);            // end of reset sequence

        // Leave attribute mode. The index register's bit 5 is the
        // palette-address-source bit: while it is clear the attribute
        // controller owns the data port and the display is blanked, so a
        // program that stops here looks like nothing was drawn at all.
        NativeMethods.outb(AttrIndex, 0x20);
    }

    private unsafe struct ModeTable { public fixed byte crtc[24]; }
    private static readonly ModeTable _mode12 = MakeMode12();

    private static ModeTable MakeMode12()
    {
        ModeTable t = default(ModeTable);
        byte* c = t.crtc;
        c[0] = 0x5F; c[1] = 0x4F; c[2] = 0x50; c[3] = 0x82;
        c[4] = 0x54; c[5] = 0x80; c[6] = 0x0B; c[7] = 0x3E;
        c[8] = 0x00; c[9] = 0x40; c[10] = 0x00; c[11] = 0x00;
        c[12] = 0x00; c[13] = 0x00; c[14] = 0x00; c[15] = 0x00;
        c[16] = 0xEA; c[17] = 0x8C; c[18] = 0xDF; c[19] = 0x28;
        c[20] = 0x00; c[21] = 0xE7; c[22] = 0x04; c[23] = 0xE3;
        return t;
    }

    // ---- public API (called by Gfx.cs) ----

    // Initialise the graphics aperture (Mode 12h on real hardware).
    public static bool Init(int width, int height)
    {
        // Program the card into Mode 12h (640x480, 16 colours).
        ProgramMode12();

        // Enable all four bit planes so writes reach them.
        SetPlaneMask(0x0F);

        // Enable the card in the text/video mode: this selects the B8000
        // window for the VGA card, at linear address 0xA0000.
        NativeMethods.outb(StatusA, 0x03);            // end of reset sequence

        // Leave attribute mode: index register's bit 5 = 1 means the CPU data
        // port writes directly to video memory rather than the attribute controller.
        NativeMethods.outb(AttrIndex, 0x20);

        Active = true;
        Clear(0);
        return true;
    }

    // Returns to the 80x25 text console the shell lives in.
    public static void Exit()
    {
        // Mode 03h CRTC values, then the standard 25-row text setup.
        for (int i = 0; i < 32; i++)
        {
            NativeMethods.outb(CrtcIndex, (byte)i);
            NativeMethods.outb(CrtcData, _textCrtc.v[i]);
        }
        NativeMethods.outb(Misc, 0x67);
        NativeMethods.outb(StatusA, 0x00);
        NativeMethods.outb(AttrIndex, 0x10); NativeMethods.outb(AttrData, 0x0C);
        NativeMethods.outb(AttrIndex, 0x04); NativeMethods.outb(AttrData, 0x00);   // allow write to index 4
        NativeMethods.outb(AttrIndex, 0x14); NativeMethods.outb(AttrData, 0x07);
        NativeMethods.outb(AttrIndex, 0x04); NativeMethods.outb(AttrData, 0x01);
        NativeMethods.outb(AttrIndex, 0x20); NativeMethods.outb(AttrData, 0x00);   // normal palette
        NativeMethods.outb(AttrIndex, 0x21); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x22); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x23); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x24); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x25); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x26); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x27); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x28); NativeMethods.outb(AttrData, 0x0F);
        NativeMethods.outb(SeqIndex, 0x01); NativeMethods.outb(SeqData, 0x00);   // character generator on
        NativeMethods.outb(SeqIndex, 0x03); NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqIndex, 0x05); NativeMethods.outb(SeqData, 0x10);   // odd/even on
        NativeMethods.outb(SeqIndex, 0x04); NativeMethods.outb(SeqData, 0x00);   // 4-bit memory mode off
        NativeMethods.outb(GfxIndex, 0x06); NativeMethods.outb(GfxData, 0x0E);   // back to the text window
        NativeMethods.outb(GfxIndex, 0x08); NativeMethods.outb(GfxData, 0x00);
        SetPlaneMask(0x0F);
        NativeMethods.outb(StatusA, 0x03);
        Active = false;
    }

    // Re-assert the state that drawing depends on.  Deliberately does NOT touch
    // the timing or mode registers: those are set once, from one table, so
    // there is a single place where the card's configuration is decided.
    public static void Reassert()
    {
        SetPlaneMask(0x0F);
        NativeMethods.outb(AttrIndex, 0x10); NativeMethods.outb(AttrData, 0x01);   // graphics, not text
        NativeMethods.outb(StatusA, 0x03);
        NativeMethods.outb(AttrIndex, 0x20);                    // leave attribute mode
        Active = true;
    }

    // ---- pixel access ----

    // The framebuffer aperture: the VGA card maps the linear address 0xA0000
    // to the segment A000:0000.  The kernel maps it via the identity mapping,
    // so the pointer is just that address cast.
    private unsafe struct CrtcTable { public fixed byte v[32]; }
    private static readonly CrtcTable _textCrtc = MakeTextCrtc();

    private static CrtcTable MakeTextCrtc()
    {
        CrtcTable t = default(CrtcTable);
        t.v[0] = 0x5F; t.v[1] = 0x4F; t.v[2] = 0x50; t.v[3] = 0x82;
        t.v[4] = 0x55; t.v[5] = 0x81; t.v[6] = 0xBF; t.v[7] = 0x1F;
        t.v[8] = 0x00; t.v[9] = 0x4F; t.v[10] = 0x0D; t.v[11] = 0x30;
        t.v[12] = 0x37; t.v[13] = 0x06; t.v[14] = 0x1C; t.v[15] = 0x0F;
        for (int i = 16; i < 32; i++) t.v[i] = 0;
        return t;
    }

    internal static byte* Plane(int n)
    {
        return (byte*)(Layout.VgaGraphBase + (ulong)n * (ulong)Layout.GfxPlaneSize);
    }

    // Selects which bit plane subsequent writes reach.
    public static void SetPlaneMask(byte mask)
    {
        NativeMethods.outb(SeqIndex, SeqMapMask);
        NativeMethods.outb(SeqData, mask);
    }

    // ---- properties ---- (matching the original Gfx.cs surface)

    public static bool Active;

    // The card's geometry, taken from the shared Layout struct (640x480 Mode 12h).
    public static int Width => Layout.GfxWidth;
    public static int Height => Layout.GfxHeight;

    // ---- 80x25 text mode CRTC values ----

    private static byte[] _textCrtcData = new byte[32];

    static LegacyVga()
    {
        _textCrtcData[0] = 0x5F; _textCrtcData[1] = 0x4F; _textCrtcData[2] = 0x50; _textCrtcData[3] = 0x82;
        _textCrtcData[4] = 0x55; _textCrtcData[5] = 0x81; _textCrtcData[6] = 0xBF; _textCrtcData[7] = 0x1F;
        _textCrtcData[8] = 0x00; _textCrtcData[9] = 0x4F; _textCrtcData[10] = 0x0D; _textCrtcData[11] = 0x30;
        _textCrtcData[12] = 0x37; _textCrtcData[13] = 0x06; _textCrtcData[14] = 0x1C; _textCrtcData[15] = 0x0F;
        for (int i = 16; i < 32; i++) _textCrtcData[i] = 0;
    }

    // ---- primitives ----

    public static void SetPixel(int x, int y, byte c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        byte* p = Plane(c & 3) + y * Layout.GfxBytesPerRow + (x >> 3);
        byte bit = (byte)(1 << (HighBit - (x & 7)));
        if ((c & 8) != 0) *p |= bit; else *p &= (byte)~bit;
    }

    public static byte GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        byte mask = (byte)(1 << (HighBit - (x & 7)));
        int off = y * Layout.GfxBytesPerRow + (x >> 3);
        byte c = 0;
        for (int n = 0; n < Layout.GfxPlaneCount; n++)
            if ((Plane(n)[off] & mask) != 0) c |= (byte)(1 << n);
        return c;
    }

    public static void Clear(byte c) { FillRect(0, 0, Width, Height, c); }

    // Fills with a solid colour.
    //
    // A colour index is one bit in one plane, so filling is two passes: clear
    // all four planes, then paint only the plane the colour needs. Painting a
    // single plane without clearing the others is the classic planar bug -
    // it works for colours 8..15 and quietly renders 0..7 as whatever the
    // planes happened to hold.
    public static void FillRect(int x, int y, int w, int h, byte c)
    {
        if (w <= 0 || h <= 0) return;
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > Width) w = Width - x;
        if (y + h > Height) h = Height - y;
        if (w <= 0 || h <= 0) return;

        byte fill = (c & 8) != 0 ? (byte)0xFF : (byte)0x00;

        if ((x & 7) != 0 || (w & 7) != 0)
        {
            // Partial bytes at the edges: correctness over speed.
            for (int r = 0; r < h; r++)
                for (int i = 0; i < w; i++)
                    SetPixel(x + i, y + r, c);
            return;
        }

        int bytes = w >> 3;
        SetPlaneMask(0x0F);
        Span(0, x, y, h, bytes);
        SetPlaneMask((byte)(1 << (c & 3)));
        Span(fill, x, y, h, bytes);
        SetPlaneMask(0x0F);
    }

    // Writes one value across a run of whole bytes.
    private static void Span(byte v, int x, int y, int h, int bytes)
    {
        byte* row0 = Plane(0) + y * Layout.GfxBytesPerRow + (x >> 3);
        for (int r = 0; r < h; r++)
        {
            byte* row = row0 + r * Layout.GfxBytesPerRow;
            for (int i = 0; i < bytes; i++) row[i] = v;
        }
    }

    public static void HLine(int x, int y, int w, byte c) { FillRect(x, y, w, 1, c); }

    public static void VLine(int x, int y, int h, byte c) { FillRect(x, y, 1, h, c); }

    public static void Rect(int x, int y, int w, int h, byte c)
    {
        if (w <= 0 || h <= 0) return;
        HLine(x, y, w, c);
        HLine(x, y + h - 1, w, c);
        VLine(x, y, h, c);
        VLine(x + w - 1, y, h, c);
    }

    // Classic raised/beveled frame: a light edge on top-left, dark on
    // bottom-right, which is what gives the Windows 3.11 windows their depth.
    public static void Bevel(int x, int y, int w, int h, byte face, byte light, byte dark)
    {
        if (w <= 0 || h <= 0) return;
        HLine(x + 1, y, w - 2, light);
        VLine(x, y + 1, h - 2, light);
        HLine(x + 1, y + h - 1, w - 2, dark);
        VLine(x + w - 1, y + 1, h - 2, dark);
        // corners
        SetPixel(x, y, face);
        SetPixel(x + w - 1, y, face);
        SetPixel(x, y + h - 1, face);
        SetPixel(x + w - 1, y + h - 1, face);
    }
}