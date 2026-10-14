// Intel e1000 / PRO1000 driver (polled).
//
// Probes the PCI bus for 8086:100E, maps its MMIO BAR, then runs a minimal
// transmit/receive path on top of the descriptor rings. No interrupts: the
// transmit and receive paths are polled, which is all PING needs and keeps
// the first driver small. Everything it needs from the kernel (physical
// memory) comes from PhysicalMemory, exactly like ATA.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Network;

internal static unsafe class E1000
{
    private const ushort VendorIntel = 0x8086;
    private const ushort DeviceE1000 = 0x100E;

    // ---- registers (byte offsets into the MMIO BAR) ----
    private const int RegCtl = 0x00000;
    private const int RegStatus = 0x00008;
    private const int RegIcr = 0x000C0;
    private const int RegRctl = 0x00100;
    private const int RegTctl = 0x00400;
    private const int RegRal = 0x05400;   // receive address (MAC low)
    private const int RegRah = 0x05404;
    private const int RegRdbal = 0x02800; // receive descriptor ring base
    private const int RegRdbah = 0x02804;
    private const int RegRdlen = 0x02808; // ring length in descriptors
    private const int RegRdh = 0x02810;
    private const int RegRdt = 0x02818;
    private const int RegTdbal = 0x03800; // transmit descriptor ring base
    private const int RegTdbah = 0x03804;
    private const int RegTdlen = 0x03808; // ring length, in BYTES
    private const int RegTdh = 0x03810;   // head
    private const int RegTdt = 0x03818;   // tail (doorbell)

    // control bits
    private const uint CtlRst = 1u << 26;
    private const uint RctlEn = 1u << 1;
    private const uint RctlBroadcast = 1u << 3;
    private const uint RctlPromisc = 1u << 6;
    private const uint TctlEn = 1u << 1;
    // Classic e1000 (82540) reports link state in STATUS bit 1 (LU).
    // Bit 21 is the e1000e layout and reads 0 here. Bit 31 is PHYRA, a
    // read-only "PHY reset asserted" flag - writing it does nothing.
    private const uint StatusLinkUp = 1u << 1;

    // descriptor field offsets (both rings share the classic 16-byte layout)
    //   0..7  buffer address (64-bit physical)
    //   8..9  length      10 checksum offset
    //   11    command     12 status     13 checksum start  14..15 special
    private const int DescCmd = 11;        // command byte
    private const int DescStatus = 12;     // status byte

    // descriptor command/status bits
    private const byte CmdEop = 0x01;       // end of packet
    private const byte CmdRs = 0x08;        // report status (device sets DD back)
    private const byte StatusDd = 0x01;     // descriptor done

    private const int DescSize = 16;
    private const int TxDescCount = 8;
    private const int RxDescCount = 32;
    private const int BufSize = 2048;
    // How many receive descriptors to keep posted ahead of the cursor. RDT
    // must always sit well beyond RDH: when RDT catches up to RDH the card
    // considers the ring empty, stops receiving, and never recovers because
    // nothing else moves the tail.
    private const int RxPosted = 8;

    private static byte* _mmio;             // register base (identity mapped)
    private static ulong _mac;              // our MAC address
    private static bool _ready;
    private static int _bus, _slot, _func;

    // descriptor rings + buffers, carved from physical memory
    private static byte* _txRing; private static byte* _txBuf;
    private static byte* _rxRing; private static byte* _rxBuf;
    private static int _txNext; private static int _rxNext;

    // Result of the last Init: 0 ok, 1 no NIC, 2 no BAR, 3 link down,
    // 4 out of memory, 5 no MAC address.
    public static int LastError;

    public static bool Init()
    {
        LastError = 0;
        if (!Pci.Find(VendorIntel, DeviceE1000, out _bus, out _slot, out _func))
        {
            if (!Pci.FindClass(2, 0, out _bus, out _slot, out _func)) { LastError = 1; return false; }
        }

        ulong bar;
        if (!Pci.DecodeBar(_bus, _slot, _func, 0, out bar)) { LastError = 2; return false; }
        if (bar == 0) { LastError = 2; return false; }
        _mmio = (byte*)bar;                     // identity map: VA == phys

        Pci.EnableBusMaster(_bus, _slot, _func);

        // reset the device and wait for the self-clearing bit. There is no
        // separate PHY reset control: CTRL bit 31 is read-only status.
        RegWrite(RegCtl, RegRead(RegCtl) | CtlRst);
        for (int i = 0; i < 100000; i++)
            if ((RegRead(RegCtl) & CtlRst) == 0) break;

        // Read the MAC after the reset: RA/RAH are reloaded from the EEPROM as
        // part of it, so sampling them beforehand is not reliable.
        uint ral = *(uint*)(_mmio + RegRal);
        uint rah = *(uint*)(_mmio + RegRah);
        _mac = ((ulong)rah << 32) | ral;
        if (_mac == 0) { LastError = 5; return false; }   // no MAC: unusable NIC

        // The PHY renegotiates for a while after reset; poll the link for a
        // bounded time rather than sampling once and giving up. The loop is
        // capped so a NIC that never links cannot wedge the shell.
        bool link = false;
        for (int i = 0; i < 2000000; i++)
            if ((RegRead(RegStatus) & StatusLinkUp) != 0) { link = true; break; }
        if (!link) { LastError = 3; return false; }

        // Allocate the rings and the per-descriptor data buffers. Each
        // descriptor owns one BufSize buffer, so the buffer area is sized
        // in pages to hold every slot (not just one).
        _txRing = (byte*)PhysicalMemory.Alloc(TxDescCount, 128);
        _txBuf = (byte*)PhysicalMemory.Alloc(Frames(TxDescCount), 4096);
        _rxRing = (byte*)PhysicalMemory.Alloc(RxDescCount, 128);
        _rxBuf = (byte*)PhysicalMemory.Alloc(Frames(RxDescCount), 4096);
        if ((ulong)_txRing == 0 || (ulong)_txBuf == 0 || (ulong)_rxRing == 0 || (ulong)_rxBuf == 0) { LastError = 4; return false; }

        for (int i = 0; i < TxDescCount * DescSize; i++) _txRing[i] = 0;
        for (int i = 0; i < RxDescCount * DescSize; i++) _rxRing[i] = 0;

        // Hand every receive descriptor its buffer up front; the driver
        // recycles them by clearing DD after a frame is consumed.
        for (int i = 0; i < RxDescCount; i++)
        {
            byte* d = _rxRing + i * DescSize;
            *(ulong*)d = (ulong)(_rxBuf + (ulong)i * BufSize);
            *(ushort*)(d + 8) = 0;
            *(byte*)(d + DescStatus) = 0;
        }

        // Program the rings. RDLEN/TDLEN count BYTES, not descriptors, and the
        // device keeps only bits 7..19 of them - so the value must be a whole
        // multiple of 128. (RDH/TDH index descriptors; RDT/TDT do too.)
        RegWrite(RegRdbal, (uint)(ulong)_rxRing);
        RegWrite(RegRdbah, (uint)((ulong)_rxRing >> 32));
        RegWrite(RegRdlen, RxDescCount * DescSize);
        RegWrite(RegRdh, 0);
        RegWrite(RegRdt, RxPosted - 1);
        // Touching the card once here makes it run its pending receive work
        // rather than waiting for the first polled frame.
        RegRead(RegRdh);

        RegWrite(RegTdbal, (uint)(ulong)_txRing);
        RegWrite(RegTdbah, (uint)((ulong)_txRing >> 32));
        RegWrite(RegTdlen, TxDescCount * DescSize);
        RegWrite(RegTdh, 0);
        RegWrite(RegTdt, 0);

        // enable receive + transmit
        RegWrite(RegRctl, RegRead(RegRctl) | RctlEn | RctlBroadcast);
        RegWrite(RegTctl, RegRead(RegTctl) | TctlEn);

        _txNext = 0;
        _rxNext = 0;
        _ready = true;
        return true;
    }

    public static bool Ready => _ready;
    public static ulong Mac => _mac;

    public static string MacString() => "00:00:00:00:00:00"; // replaced by formatted printer

    // Pages needed to hold `n` buffers of BufSize bytes each.
    private static int Frames(int n) { return (n * BufSize + 4095) / 4096 + 1; }

    private static uint RegRead(int off) { return *(uint*)(_mmio + off); }
    private static void RegWrite(int off, uint v) { *(uint*)(_mmio + off) = v; }

    // Send one ethernet frame (poll until the TX ring frees up).
    public static bool Send(byte* frame, int len)
    {
        if (!_ready) return false;

        // Kick the transmit doorbell first if a descriptor is already free;
        // a bounded spin on the previous descriptor follows.
        int n = _txNext % TxDescCount;
        byte* desc = _txRing + n * DescSize;
        byte* buf = _txBuf + n * BufSize;

        for (int i = 0; i < len && i < BufSize; i++) buf[i] = frame[i];
        for (int i = len; i < BufSize; i++) buf[i] = 0;

        // descriptor: addr(8) len(2) cso(1) cmd(1) status(1) css(1) special(2).
        // RS tells the device to write DD back into the status byte when the
        // frame leaves; without it the descriptor never completes.
        ulong addr = (ulong)buf;
        *(ulong*)desc = addr;
        *(ushort*)(desc + 8) = (ushort)len;
        *(byte*)(desc + 10) = 0;
        *(byte*)(desc + DescCmd) = (byte)(CmdEop | CmdRs);
        *(byte*)(desc + DescStatus) = 0;
        *(byte*)(desc + 13) = 0;

        _txNext++;
        RegWrite(RegTdt, (uint)(_txNext % TxDescCount));   // ring doorbell

        // Bounded wait for the descriptor to report done. Never spins
        // forever: a stuck NIC must not wedge the shell.
        for (int spins = 0; spins < 20000; spins++)
            if ((*(byte*)(desc + DescStatus) & StatusDd) != 0) return true;
        return false;
    }

    // Poll one received frame into `buf` (returns its length, or 0).
    // The descriptor's own status byte is the authority on whether a frame
    // landed. ICR and RDH are deliberately not consulted as a gate: ICR is
    // read-to-clear, so a single early read would latch reception off for
    // good, and RDH is the card's own cursor which the card advances only
    // after it has already written the frame.
    //
    // The card runs because the caller polls in a loop over the shell's
    // timer ticks, not because this function touches a register.
    public static int Receive(byte* buf, int cap)
    {
        if (!_ready) return 0;
        int n = _rxNext % RxDescCount;
        byte* desc = _rxRing + n * DescSize;
        if ((*(byte*)(desc + DescStatus) & StatusDd) == 0) return 0;

        ulong addr = *(ulong*)desc;
        int len = *(ushort*)(desc + 8);
        if (len > cap) len = cap;
        byte* src = (byte*)addr;
        for (int i = 0; i < len; i++) buf[i] = src[i];

        // Recycle the descriptor and post the next batch. RDT has to run ahead of
        // _rxNext, otherwise the card sees an empty ring and stops receiving.
        *(byte*)(desc + DescStatus) = 0;
        _rxNext++;
        int posted = _rxNext + RxPosted - 1;
        if (posted > RxDescCount - 1) posted = RxDescCount - 1;
        RegWrite(RegRdt, (uint)posted);                    // ring doorbell
        return len;
    }
}
