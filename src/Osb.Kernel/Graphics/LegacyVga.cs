// Legacy VGA path: the classic Mode 12h (640x480, 16 colours).
//
// The card keeps its memory as four bit planes that all live at the same
// addresses: a byte write to A0000 lands in whichever planes the sequencer's
// map mask enables, and a byte read returns the plane the graphics
// controller's read-map-select points at. There is no separate per-plane
// address; the colour is assembled one bit per plane.
//
// This is the path for machines without the Bochs VBE device. Under QEMU the
// linear framebuffer in Bochs.cs is preferred and selected first; Mode 12h is
// only reached when that device is absent.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Graphics;

internal static unsafe partial class LegacyVga
{
    // Sequencer / CRT controller / graphics controller / attribute ports.
    private const ushort SeqIndex = 0x3C4;
    private const ushort SeqData = 0x3C5;
    private const ushort CrtcIndex = 0x3D4;
    private const ushort CrtcData = 0x3D5;
    private const ushort GfxIndex = 0x3CE;
    private const ushort GfxData = 0x3CF;
    private const ushort AttrIndex = 0x3C0;
    // The attribute controller's data register is written on the *same* port as
    // the index: each write to 0x3C0 alternates index, data, index, data... The
    // 0x3C1 port only reads the last data byte. Sending data to 0x3C1 instead
    // leaves the flip-flop stuck expecting data, so the next index byte is
    // consumed as data and the whole palette is written to the wrong entries.
    private const ushort AttrData = 0x3C0;
    private const ushort Misc = 0x3C2;
    private const ushort StatusA = 0x3DA;

    private const byte SeqMapMask = 0x02;   // sequencer: write plane enable
    private const byte GrcReadMap = 0x04;   // graphics controller: read plane
    private const int HighBit = 7;          // bit of the leftmost pixel in a byte

    public static bool Active;

    // Mode 12h geometry, from the shared Layout constants.
    public static int Width => Layout.GfxWidth;
    public static int Height => Layout.GfxHeight;

    // ---- mode control ----

    // While VBE is enabled the card ignores the legacy sequencer/CRTC writes:
    // the mode looks like it took (the DISPI registers report it) while the
    // framebuffer is never scanned out. Turning VBE off first makes the writes
    // below mean what they say. The DISPI data port is 16 bits wide, so the
    // disable is a word write, not two bytes.
    private static void DisableVbe()
    {
        NativeMethods.outw(0x1CE, 0x04);        // VBE_DISPI_INDEX_ENABLE
        NativeMethods.outw(0x1CF, 0x00);
    }

    // Program the card into Mode 12h (640x480, 16 colours).
    private static void ProgramMode12()
    {
        DisableVbe();

        // Sequencer: reset, 8-dot characters, all four planes writable,
        // sequential addressing (no odd/even or chain-4), then run.
        NativeMethods.outb(SeqIndex, 0x00); NativeMethods.outb(SeqData, 0x01);
        NativeMethods.outb(SeqIndex, 0x01); NativeMethods.outb(SeqData, 0x01);
        NativeMethods.outb(SeqIndex, 0x02); NativeMethods.outb(SeqData, 0x0F);
        NativeMethods.outb(SeqIndex, 0x03); NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqIndex, 0x04); NativeMethods.outb(SeqData, 0x06);
        NativeMethods.outb(SeqIndex, 0x00); NativeMethods.outb(SeqData, 0x03);

        // Graphics controller: read mode 0, write mode 0, the A0000 64K
        // window (GR06 bit 2 set), colour-don't-care off.
        NativeMethods.outb(GfxIndex, 0x00); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x01); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x02); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x03); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x04); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x05); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x06); NativeMethods.outb(GfxData, 0x05);
        NativeMethods.outb(GfxIndex, 0x07); NativeMethods.outb(GfxData, 0x0F);
        NativeMethods.outb(GfxIndex, 0x08); NativeMethods.outb(GfxData, 0xFF);

        // Attribute controller: identity palette map (pixel value -> DAC
        // entry), graphics mode, no blink.
        NativeMethods.inb(StatusA);             // reset the address/data flip-flop
        for (byte i = 0; i < 16; i++) { NativeMethods.outb(AttrIndex, i); NativeMethods.outb(AttrData, i); }
        NativeMethods.outb(AttrIndex, 0x10); NativeMethods.outb(AttrData, 0x01);
        NativeMethods.outb(AttrIndex, 0x11); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x12); NativeMethods.outb(AttrData, 0x0F);
        NativeMethods.outb(AttrIndex, 0x13); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x14); NativeMethods.outb(AttrData, 0x00);

        // Misc output: 25MHz dot clock, negative sync.
        NativeMethods.outb(Misc, 0xE3);

        // CRTC timing. Unlock registers 0-7 first (register 0x11 bit 7).
        NativeMethods.outb(CrtcIndex, 0x11); NativeMethods.outb(CrtcData, 0x0C);
        for (int i = 0; i < 25; i++)
        {
            NativeMethods.outb(CrtcIndex, (byte)i);
            NativeMethods.outb(CrtcData, _mode12Crtc.v[i]);
        }

        // Leave attribute mode; the display is blanked until this is done.
        NativeMethods.outb(AttrIndex, 0x20);
    }

    public static bool Init(int width, int height)
    {
        ProgramMode12();
        SetPlaneMask(0x0F);
        Active = true;
        Clear(0);
        return true;
    }

    // Re-assert the state that drawing depends on. Deliberately does not touch
    // the timing or mode registers: those are set once, from one table, so
    // there is a single place where the card's configuration is decided.
    public static void Reassert()
    {
        SetPlaneMask(0x0F);
        NativeMethods.outb(AttrIndex, 0x20);    // leave attribute mode
        Active = true;
    }

    // ---- character generator (text font) ----
    //
    // The 8x16 text glyphs live in plane 2 of the card's memory, and graphics
    // modes reuse that same plane for pixels. Programming any graphics mode
    // therefore destroys the font: on the way back the console gets the correct
    // 80x25 timing but no glyphs, so the screen looks blank except for the
    // CRTC-drawn cursor. Stash the table before going graphic and put it back
    // on the way out.

    public static void SaveFont()
    {
        if (_fontSaved) return;
        BeginFontWindow();
        byte* src = FontBase;
        for (int i = 0; i < FontBytes; i++) _fontStore.v[i] = src[i];
        EndFontWindow();
        _fontSaved = true;
    }

    public static void RestoreFont()
    {
        if (!_fontSaved) return;
        BeginFontWindow();
        byte* dst = FontBase;
        for (int i = 0; i < FontBytes; i++) dst[i] = _fontStore.v[i];
        EndFontWindow();
        _fontSaved = false;
    }

    // Maps the flat text window onto plane 2 (the font plane). Text mode keeps
    // the CPU window at B8000 (graphics-controller register 6, bits 2-3 = 3), and
    // the odd/even split that normally makes B8000 read characters+attributes is
    // selected by graphics-controller register 5 bit 4, so both are adjusted
    // here; the memory-map register itself is left at the text value.
    private static void BeginFontWindow()
    {
        NativeMethods.outb(GfxIndex, 0x05); NativeMethods.outb(GfxData, 0x00);   // odd/even off: linear window
        NativeMethods.outb(GfxIndex, 0x04); NativeMethods.outb(GfxData, 0x02);   // reads return plane 2
        NativeMethods.outb(GfxIndex, 0x06); NativeMethods.outb(GfxData, 0x0E);   // keep the B8000 window
        NativeMethods.outb(SeqIndex, 0x02); NativeMethods.outb(SeqData, 0x04);   // writes reach plane 2
    }

    // Puts the graphics controller back into the text-mode map.
    private static void EndFontWindow()
    {
        NativeMethods.outb(SeqIndex, 0x02); NativeMethods.outb(SeqData, 0x03);
        NativeMethods.outb(GfxIndex, 0x04); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x05); NativeMethods.outb(GfxData, 0x10);   // odd/even (text)
        NativeMethods.outb(GfxIndex, 0x06); NativeMethods.outb(GfxData, 0x0E);
        SetPlaneMask(0x0F);
    }

    // Returns to the 80x25 text console the shell lives in.
    //
    // The register set is the classic BIOS Mode 03h table. Order matters: the
    // CRTC timing registers 0-7 are protected by register 0x11 bit 7, so they
    // are unlocked first; the sequencer is held in reset while the timing
    // changes; and all 25 CRTC registers are written, because leaving the
    // vertical timing (0x10-0x18) at the graphics values produces no sync.
    public static void Exit()
    {
        NativeMethods.outb(CrtcIndex, 0x11); NativeMethods.outb(CrtcData, 0x0C);   // unlock 0-7

        NativeMethods.outb(SeqIndex, 0x00); NativeMethods.outb(SeqData, 0x01);     // reset

        NativeMethods.outb(Misc, 0x67);

        for (int i = 0; i < 25; i++)
        {
            NativeMethods.outb(CrtcIndex, (byte)i);
            NativeMethods.outb(CrtcData, _textCrtc.v[i]);
        }

        // Sequencer: 8-dot characters, plane 0/1 (text), odd/even memory.
        NativeMethods.outb(SeqIndex, 0x01); NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqIndex, 0x02); NativeMethods.outb(SeqData, 0x03);
        NativeMethods.outb(SeqIndex, 0x03); NativeMethods.outb(SeqData, 0x00);
        NativeMethods.outb(SeqIndex, 0x04); NativeMethods.outb(SeqData, 0x02);

        // Graphics controller: the B8000 text window, read plane 0.
        NativeMethods.outb(GfxIndex, 0x00); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x01); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x02); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x03); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x04); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x05); NativeMethods.outb(GfxData, 0x10);
        NativeMethods.outb(GfxIndex, 0x06); NativeMethods.outb(GfxData, 0x0E);
        NativeMethods.outb(GfxIndex, 0x07); NativeMethods.outb(GfxData, 0x00);
        NativeMethods.outb(GfxIndex, 0x08); NativeMethods.outb(GfxData, 0xFF);

        // Attribute controller: reset the flip-flop, load the 16-entry text
        // palette and text attribute mode, then re-enable video.
        NativeMethods.inb(StatusA);
        for (byte i = 0; i < 16; i++) { NativeMethods.outb(AttrIndex, i); NativeMethods.outb(AttrData, _textAttr.v[i]); }
        NativeMethods.outb(AttrIndex, 0x10); NativeMethods.outb(AttrData, 0x0C);
        NativeMethods.outb(AttrIndex, 0x11); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x12); NativeMethods.outb(AttrData, 0x0F);
        NativeMethods.outb(AttrIndex, 0x13); NativeMethods.outb(AttrData, 0x08);
        NativeMethods.outb(AttrIndex, 0x14); NativeMethods.outb(AttrData, 0x00);
        NativeMethods.outb(AttrIndex, 0x20);

        NativeMethods.outb(SeqIndex, 0x00); NativeMethods.outb(SeqData, 0x03);     // run
        SetPlaneMask(0x0F);
        Active = false;
    }

    // ---- planar access ----

    private static byte* Base { get { return (byte*)Layout.VgaGraphBase; } }

    // The text-mode CPU window: the character generator (plane 2) is read and
    // written through here, not through the graphics aperture at A0000.
    private static byte* FontBase { get { return (byte*)Layout.VgaBase; } }

    // Selects which bit planes subsequent byte writes reach.
    public static void SetPlaneMask(byte mask)
    {
        NativeMethods.outb(SeqIndex, SeqMapMask);
        NativeMethods.outb(SeqData, mask);
    }

    // Selects which plane subsequent byte reads return.
    private static void SelectReadPlane(int n)
    {
        NativeMethods.outb(GfxIndex, GrcReadMap);
        NativeMethods.outb(GfxData, (byte)n);
    }

    // ---- register tables ----

    private const int FontBytes = 8192;     // 512 glyphs x 16 scan lines

    private unsafe struct FontStore { public fixed byte v[FontBytes]; }
    private static FontStore _fontStore;
    private static bool _fontSaved;

    private unsafe struct CrtcTable { public fixed byte v[32]; }
    private static readonly CrtcTable _mode12Crtc = MakeMode12Crtc();
    private static readonly CrtcTable _textCrtc = MakeTextCrtc();

    private static CrtcTable MakeMode12Crtc()
    {
        CrtcTable t = default(CrtcTable);
        // Standard Mode 12h CRTC table (registers 0x00-0x18).
        t.v[0] = 0x5F; t.v[1] = 0x4F; t.v[2] = 0x50; t.v[3] = 0x82;
        t.v[4] = 0x54; t.v[5] = 0x80; t.v[6] = 0x0B; t.v[7] = 0x3E;
        t.v[8] = 0x00; t.v[9] = 0x40; t.v[10] = 0x00; t.v[11] = 0x00;
        t.v[12] = 0x00; t.v[13] = 0x00; t.v[14] = 0x00; t.v[15] = 0x00;
        t.v[16] = 0xEA; t.v[17] = 0x0C; t.v[18] = 0xDF; t.v[19] = 0x28;
        t.v[20] = 0x00; t.v[21] = 0xE7; t.v[22] = 0x04; t.v[23] = 0xE3;
        t.v[24] = 0xFF;
        return t;
    }

    private static CrtcTable MakeTextCrtc()
    {
        CrtcTable t = default(CrtcTable);
        // Standard BIOS Mode 03h CRTC table (registers 0x00-0x18).
        t.v[0] = 0x5F; t.v[1] = 0x4F; t.v[2] = 0x50; t.v[3] = 0x82;
        t.v[4] = 0x55; t.v[5] = 0x81; t.v[6] = 0xBF; t.v[7] = 0x1F;
        t.v[8] = 0x00; t.v[9] = 0x4F; t.v[10] = 0x0D; t.v[11] = 0x0E;
        t.v[12] = 0x00; t.v[13] = 0x00; t.v[14] = 0x00; t.v[15] = 0x00;
        t.v[16] = 0x9C; t.v[17] = 0x0E; t.v[18] = 0x8F; t.v[19] = 0x28;
        t.v[20] = 0x1F; t.v[21] = 0x96; t.v[22] = 0xB9; t.v[23] = 0xA3;
        t.v[24] = 0xFF;
        return t;
    }

    // The 16 text-mode palette entries the attribute controller starts from.
    private unsafe struct AttrTable { public fixed byte v[16]; }
    private static readonly AttrTable _textAttr = MakeTextAttr();

    private static AttrTable MakeTextAttr()
    {
        AttrTable t = default(AttrTable);
        t.v[0] = 0x00; t.v[1] = 0x01; t.v[2] = 0x02; t.v[3] = 0x03;
        t.v[4] = 0x04; t.v[5] = 0x05; t.v[6] = 0x14; t.v[7] = 0x07;
        t.v[8] = 0x38; t.v[9] = 0x39; t.v[10] = 0x3A; t.v[11] = 0x3B;
        t.v[12] = 0x3C; t.v[13] = 0x3D; t.v[14] = 0x3E; t.v[15] = 0x3F;
        return t;
    }
}
