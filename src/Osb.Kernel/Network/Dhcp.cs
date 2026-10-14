// Minimal DHCP client: DISCOVER -> OFFER -> REQUEST -> ACK.
//
// Enough to get an address, a netmask, a gateway and a nameserver. There is
// no lease renewal and no persistence across boots: the client runs at bring-up
// and again on demand from the DHCP command.
//
// Before a lease the interface has no address, so the exchange starts from
// 0.0.0.0 to 255.255.255.255 and every step is bounded. If no server answers
// the caller keeps the static fallback rather than waiting forever.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Network;

internal static unsafe class Dhcp
{
    private const ushort ServerPort = 67;
    private const ushort ClientPort = 68;
    private const int FixedLen = 240;          // BOOTP header plus magic cookie
    private const int MaxOptions = 60;
    private const int BufFrames = 1;

    // BOOTP opcodes.
    private const byte OpRequest = 1;
    private const byte OpReply = 2;

    // Option numbers.
    private const byte OptPad = 0;
    private const byte OptSubnet = 1;
    private const byte OptRouter = 3;
    private const byte OptDns = 6;
    private const byte OptHostName = 12;
    private const byte OptRequestList = 55;
    private const byte OptLeaseTime = 51;
    private const byte OptMessageType = 53;
    private const byte OptServerId = 54;
    private const byte OptParamRequestIp = 50;
    private const byte OptEnd = 255;

    // Message types.
    private const byte MsgDiscover = 1;
    private const byte MsgOffer = 2;
    private const byte MsgRequest = 3;
    private const byte MsgAck = 5;
    private const byte MsgNak = 6;

    private static byte* _tx;
    private static byte* _rx;
    private static ulong _xid;

    // Outcome of the last run, for the shell to report.
    public static int LastResult;
    public static uint OfferIp;
    public static uint DhcpServer;

    // Why the last run ended. 0 ok, 1 no offer, 2 no ack, 3 no memory.
    private static int _fail;

    public static int Fail => _fail;

    // Obtain a lease and apply it. Returns false if no server responded, in
    // which case the caller keeps whatever addressing it already had.
    public static bool Run()
    {
        LastResult = 0;
        OfferIp = 0;
        if (!NetStack.Up && !NetStack.Init()) { _fail = 3; return false; }

        if ((ulong)_tx == 0)
        {
            _tx = (byte*)PhysicalMemory.Alloc(BufFrames, 4096);
            _rx = (byte*)PhysicalMemory.Alloc(BufFrames, 4096);
        }
        if ((ulong)_tx == 0 || (ulong)_rx == 0) { _fail = 3; return false; }

        _xid = ((ulong)NetStack.NextIpId() << 16) | 0x4F53UL;

        // Two attempts at the full exchange. Retransmission is what makes DHCP
        // reliable when a datagram is lost, and two tries keeps bring-up quick.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Build(MsgDiscover, 0, 0, 0, NetStack.Broadcast, false);
            if (WaitFor(MsgOffer, 150) != 0)
            {
                // The REQUEST goes out from the address just offered, unicast
                // to the server, with ciaddr set. That is the RENEW form.
                // The RFC's SELECTING form (ciaddr 0, broadcast from 0.0.0.0)
                // is ignored by the server shipped with the emulator.
                Build(MsgRequest, _offered, _serverId, _offered, _serverId, true);
                if (WaitFor(MsgAck, 150) != 0)
                {
                    Apply();
                    _fail = 0;
                    return true;
                }
                _fail = 2;
                return false;
            }
            _fail = 1;
        }
        return false;
    }

    // ---- building ----

    private static void Build(byte msgType, uint requestedIp, uint serverId,
                              uint srcIp, uint dstIp, bool unicast)
    {
        for (int i = 0; i < FixedLen + MaxOptions; i++) _tx[i] = 0;
        byte* b = _tx;

        b[0] = OpRequest;
        b[1] = 1;                                       // hardware: ethernet
        b[2] = 6;                                       // hardware address length
        b[3] = 0;                                       // hops
        NetStack.WriteBe32(b + 4, (uint)_xid);
        NetStack.WriteBe16(b + 8, 0);                   // seconds elapsed
        // Broadcast flag only while we have no address to be reached at.
        NetStack.WriteBe16(b + 10, unicast ? (uint)0 : (uint)0x8000);
        // ciaddr: the address we already hold, zero while selecting.
        NetStack.WriteBe32(b + 12, unicast ? srcIp : 0);

        NetStack.WriteMac(b + 28, E1000.Mac);           // client hardware address
        b[236] = 99; b[237] = 130; b[238] = 83; b[239] = 99;   // magic cookie

        int o = FixedLen;
        _tx[o++] = OptMessageType; _tx[o++] = 1; _tx[o++] = msgType;

        // Ask for the three settings the stack actually uses.
        _tx[o++] = OptRequestList; _tx[o++] = 3;
        _tx[o++] = OptSubnet; _tx[o++] = OptRouter; _tx[o++] = OptDns;

        if (requestedIp != 0)
        {
            _tx[o++] = OptParamRequestIp; _tx[o++] = 4;
            NetStack.WriteBe32(_tx + o, requestedIp); o += 4;
        }
        if (serverId != 0)
        {
            _tx[o++] = OptServerId; _tx[o++] = 4;
            NetStack.WriteBe32(_tx + o, serverId); o += 4;
        }
        _tx[o++] = OptLeaseTime; _tx[o++] = 4;         // ask for a 3600 s lease
        NetStack.WriteBe32(_tx + o, 3600); o += 4;
        _tx[o++] = OptHostName; _tx[o++] = 4;
        NetStack.WriteBe32(_tx + o, 0x4F5342); o += 4;  // "OSB"
        _tx[o++] = OptEnd;

        // The unicast case goes straight to the server's MAC, which the offer told
        // us; resolving it over ARP would need an address we do not have yet.
        if (unicast && _serverMac != 0) NetStack.LearnMac(dstIp, _serverMac);
        NetStack.SendUdp(srcIp, dstIp, ClientPort, ServerPort, _tx, o);
    }

    // ---- receiving ----

    // Wait for a DHCP message of `want` type, `ticks` long. Returns the
    // offered address, or 0 on timeout.
    private static uint WaitFor(byte want, int ticks)
    {
        ulong deadline = Osb.Kernel.Cpu.Pit.Ticks + (ulong)ticks;
        // The loop halts rather than spinning: a busy loop on this scale burns
        // the whole window in far less than the tick budget it is waiting for,
        // so the reply never arrives in time. HLT parks the CPU until the next
        // tick, which both keeps time honest and lets the card run.
        // Bounded by iteration count as a backstop in case the timer stops.
        for (int i = 0; i < 4096; i++)
        {
            uint got = Read(want);
            if (got != 0) return got;
            if (Osb.Kernel.Cpu.Pit.Ticks >= deadline) return 0;
            Osb.Kernel.Hardware.NativeMethods.halt();
        }
        return 0;
    }

    // Pull one frame and decode it if it is a DHCP message of the wanted type.
    private static uint Read(byte want)
    {
        int len = NetStack.Pump();
        if (len < 14 + 20 + 8) return 0;
        byte* f = NetStack.RxBuffer;
        if (NetStack.ReadBe16(f + 12) != NetStack.EtherIp) return 0;

        byte* ip = f + 14;
        if ((ip[0] >> 4) != 4) return 0;
        if (ip[9] != 17) return 0;
        int ihl = (ip[0] & 0x0F) * 4;

        byte* udp = ip + ihl;
        if (NetStack.ReadBe16(udp + 2) != ClientPort) return 0;

        // The message length comes from the UDP header, not from what the card
        // happened to deliver: the option area starts at offset 240, so a
        // length clamped to the fixed part would discard every option and the
        // message type with it.
        int udpLen = NetStack.ReadBe16(udp + 4);
        int blen = udpLen - 8;
        if (blen > FixedLen + MaxOptions) blen = FixedLen + MaxOptions;
        if (blen < FixedLen) return 0;

        byte* b = udp + 8;

        if (b[0] != OpReply) return 0;
        if (NetStack.ReadBe32(b + 4) != (uint)_xid) return 0;
        if (b[236] != 99 || b[237] != 130 || b[238] != 83 || b[239] != 99) return 0;

        uint offered = NetStack.ReadBe32(b + 16);       // yiaddr
        uint server = NetStack.ReadBe32(b + 20);        // siaddr
        if (server == 0) server = NetStack.ReadBe32(b + 24);

        byte msgType = 0;
        uint serverId = 0;
        uint mask = 0, gateway = 0, dns = 0;
        int o = FixedLen;
        while (o < blen)
        {
            byte code = b[o++];
            if (code == OptPad) continue;
            if (code == OptEnd) break;
            if (o >= blen) break;
            int len8 = b[o++];
            if (o + len8 > blen) break;
            if (code == OptMessageType && len8 >= 1) msgType = b[o];
            else if (code == OptServerId && len8 >= 4) serverId = NetStack.ReadBe32(b + o);
            else if (code == OptSubnet && len8 >= 4) mask = NetStack.ReadBe32(b + o);
            else if (code == OptRouter && len8 >= 4) gateway = NetStack.ReadBe32(b + o);
            else if (code == OptDns && len8 >= 4) dns = NetStack.ReadBe32(b + o);
            o += len8;
        }
        if (msgType != want) return 0;
        if (offered == 0) return 0;

        // Keep what the message carried. The receive buffer is overwritten by
        // the next frame, so these cannot be re-read from it later.
        _offered = offered;
        _mask = mask; _gateway = gateway; _dns = dns;
        _serverId = serverId != 0 ? serverId : server;
        // The server's MAC is the ethernet source of the frame it just sent.
        // Learning it here saves an ARP round-trip once we have an address.
        _serverMac = NetStack.ReadMac(f + 6);
        return offered;
    }

    private static uint _offered, _mask, _gateway, _dns, _serverId;
    private static ulong _serverMac;

    // ---- applying ----

    private static void Apply()
    {
        uint mask = _mask != 0 ? _mask : NetStack.DefaultNetmask;
        uint gateway = _gateway != 0 ? _gateway : NetStack.DefaultGateway;
        uint dns = _dns != 0 ? _dns : NetStack.DefaultDns;

        OfferIp = _offered;
        DhcpServer = _serverId;
        NetStack.Configure(_offered, mask, gateway, dns, true);
        if (_serverMac != 0) NetStack.LearnMac(gateway, _serverMac);
    }
}