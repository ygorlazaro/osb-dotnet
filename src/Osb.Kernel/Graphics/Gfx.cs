// VGA graphics mode 12h: 640x480, 16 colours.
//
// The card keeps memory as four bit planes, one per bit of the colour index.
// A pixel is a single bit in one of them, so the plane is selected through
// the sequencer's write-plane mask and then ordinary byte writes to the
// window at A0000 land in the right plane.
//
// Everything here is bounded and allocation-free; the framebuffer is reached
// through the identity map, so no paging work is needed.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe class Gfx
{
    public const int Width = Layout.GfxWidth;
    public const int Height = Layout.GfxHeight;

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

    private const byte SeqMapMask = 0x0E;   // write plane enable

    // Bit index of the leftmost pixel inside a byte.
    private const int HighBit = 7;

    public static bool Active;

    private static byte* Plane(int n)
    {
        return (byte*)(Layout.VgaGraphBase + (ulong)n * (ulong)Layout.GfxPlaneSize);
    }

    private static void SeqSelect(byte reg) { NativeMethods.outb(SeqIndex, reg); }
    private static void CrtcSelect(byte reg) { NativeMethods.outb(CrtcIndex, reg); }
    private static void GfxSelect(byte reg) { NativeMethods.outb(GfxIndex, reg); }

    // Selects which bit planes subsequent writes reach.
    public static void SetPlaneMask(byte mask)
    {
        SeqSelect(SeqMapMask);
        NativeMethods.outb(SeqData, mask);
    }

    // KNOWN INCOMPLETE: the card programs mode 12h and the aperture is
    // verified read/write correct from the guest side, but the framebuffer is
    // not yet scanned out - QEMU still renders its text console.
    //
    // Established so far:
    //   * The legacy aperture decodes only when the sequencer has 4-bit memory
    //     mode set AND GR06 bits 2-3 are 00; GR06 = 0x0E selects the B8000
    //     text window instead, and writes to A0000 then vanish. Fixed.
    //   * Writes must also be released from attribute mode (index register
    //     bit 5), or the display stays blanked.
    //   * VBE is left enabled by the firmware and must be turned off first,
    //     otherwise the model reads a separate copy of the sequencer
    //     registers and every legacy write is ignored.
    //   * A graphical display backend is required to see anything at all.
    //     With "-display none" or "-vnc", screendump returns a byte-identical
    //     frame no matter what the guest draws. "-display egl-headless"
    //     reflects guest state correctly (verified against the text console).
    //
    // Remaining suspect: the 640x480 CRTC timing table. Register values of
    // 0x00..0xFF do not produce that geometry - QEMU derives a height of
    // 787 from them - so the table itself needs replacing with a real one.

    // ---- mode control ----

    // Mode 12h CRTC timing: the register values are simply 0..0xFF, which is
    // what makes this mode easy to program exactly.
    // Mode 12h register values, taken from SeaBIOS's own table for mode 0x12
    // (vgasrc/stdvgamodes.c: sequ_0e, crtc_11, actl_10, grdc_0d). Using the
    // firmware's table rather than a remembered one is the difference between
    // a mode that scans out and one that quietly does not.
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

    private static void ProgramMode12()
    {
        DisableVbe();

        // Sequencer: clocks, all four planes writable, character generator,
        // memory mode.
        SeqSelect(0x00); NativeMethods.outb(SeqData, 0x01);
        SeqSelect(0x01); NativeMethods.outb(SeqData, 0x0F);
        SeqSelect(0x02); NativeMethods.outb(SeqData, 0x00);
        SeqSelect(0x03); NativeMethods.outb(SeqData, 0x00);
        SeqSelect(0x04); NativeMethods.outb(SeqData, 0x00);
        SeqSelect(0x05); NativeMethods.outb(SeqData, 0x00);
        SeqSelect(0x06); NativeMethods.outb(SeqData, 0x0E);
        SeqSelect(0x07); NativeMethods.outb(SeqData, 0x0D);

        // Graphics controller. GR06 selects the graphics display and the
        // memory map; the usual 0x0E would point the card at the B8000 text
        // window, where writes to the framebuffer are simply dropped.
        GfxSelect(0x00); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x01); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x02); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x03); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x04); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x05); NativeMethods.outb(GfxData, 0x00);
        GfxSelect(0x06); NativeMethods.outb(GfxData, 0x05);
        GfxSelect(0x07); NativeMethods.outb(GfxData, 0x0F);
        GfxSelect(0x08); NativeMethods.outb(GfxData, 0xFF);

        // Attribute controller: sixteen palette entries, then mode control.
        NativeMethods.outb(StatusA, 0x00);
        for (byte i = 0; i < 16; i++) { AttrSelect(i); NativeMethods.outb(AttrData, i); }
        AttrSelect(0x10); NativeMethods.outb(AttrData, 0x01);
        AttrSelect(0x11); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x12); NativeMethods.outb(AttrData, 0x0F);
        AttrSelect(0x13); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x14); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x15); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x16); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x17); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x18); NativeMethods.outb(AttrData, 0x0E);
        AttrSelect(0x19); NativeMethods.outb(AttrData, 0x04);

        NativeMethods.outb(Misc, 0xE3);

        for (int i = 0; i < 24; i++)
        {
            CrtcSelect((byte)i);
            NativeMethods.outb(CrtcData, _mode12.crtc[i]);
        }

        NativeMethods.outb(StatusA, 0x03);            // end of reset sequence

        // Leave attribute mode. The index register's bit 5 is the
        // palette-address-source bit: while it is clear the attribute
        // controller owns the data port and the display is blanked, so a
        // program that stops here looks like nothing was drawn at all.
        NativeMethods.outb(AttrIndex, 0x20);
    }

    // The VESA BIOS Extension registers, on the standard 0x1CE/0x1CF pair.
    // The "Bochs display" interface is what makes them reachable without the
    // BIOS INT 10h entry point.
    private const ushort VbeIndex = 0x1CE;
    private const ushort VbeData = 0x1CF;
    private const byte DispiIndexPort = 0x04;      // select which DISPI register
    private const byte DispiEnable = 0x04;        // DISPI index of ENABLE
    private const byte DispiEnabled = 0x01;

    private static void AttrSelect(byte reg) { NativeMethods.outb(AttrIndex, reg); }

    // Forces the card back to plain legacy VGA.
    //
    // While VBE is enabled the model reads a SEPARATE copy of the sequencer
    // registers, so every legacy write is quietly ignored: the mode appears to
    // take effect (the card reports a new resolution) while the framebuffer is
    // never scanned out. Turning VBE off first makes the register writes below
    // mean what they say.
    private static void DisableVbe()
    {
        NativeMethods.outb(VbeIndex, DispiIndexPort);
        NativeMethods.outb(VbeData, DispiEnable);
        NativeMethods.outb(VbeData, 0x00);        // ENABLE = 0
    }

    public static void Enter()
    {
        ProgramMode12();
        SetPlaneMask(0x0F);
        Active = true;
        Clear(0);
    }

    // Re-asserts the state that drawing depends on. Deliberately does NOT touch
    // the timing or mode registers: those are set once, from one table, so
    // there is a single place where the card's configuration is decided.
    public static void Reassert()
    {
        SetPlaneMask(0x0F);
        AttrSelect(0x10); NativeMethods.outb(AttrData, 0x01);   // graphics, not text
        NativeMethods.outb(StatusA, 0x03);
        NativeMethods.outb(AttrIndex, 0x20);                    // leave attribute mode
    }

    // Back to 80x25 text, where the shell lives.
    public static void Exit()
    {
        // Mode 03h CRTC values, then the standard 25-row text setup.
        for (int i = 0; i < 32; i++)
        {
            CrtcSelect((byte)i);
            NativeMethods.outb(CrtcData, _textCrtc.v[i]);
        }
        NativeMethods.outb(Misc, 0x67);
        NativeMethods.outb(StatusA, 0x00);
        AttrSelect(0x10); NativeMethods.outb(AttrData, 0x0C);
        AttrSelect(0x04); NativeMethods.outb(AttrData, 0x00);   // allow write to index 4
        AttrSelect(0x14); NativeMethods.outb(AttrData, 0x07);
        AttrSelect(0x04); NativeMethods.outb(AttrData, 0x01);
        AttrSelect(0x20); NativeMethods.outb(AttrData, 0x00);   // normal palette
        AttrSelect(0x21); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x22); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x23); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x24); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x25); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x26); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x27); NativeMethods.outb(AttrData, 0x00);
        AttrSelect(0x28); NativeMethods.outb(AttrData, 0x0F);
        SeqSelect(0x01); NativeMethods.outb(SeqData, 0x00);   // character generator on
        SeqSelect(0x03); NativeMethods.outb(SeqData, 0x00);
        SeqSelect(0x05); NativeMethods.outb(SeqData, 0x10);   // odd/even on
        SeqSelect(0x04); NativeMethods.outb(SeqData, 0x00);   // 4-bit memory mode off
        GfxSelect(0x06); NativeMethods.outb(GfxData, 0x0E);   // back to the text window
        GfxSelect(0x08); NativeMethods.outb(GfxData, 0x00);
        SetPlaneMask(0x0F);
        NativeMethods.outb(StatusA, 0x03);
        Active = false;
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

    // Mode 03h CRTC values, as a fixed buffer: this is freestanding code and
    // must not allocate managed arrays.
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
}