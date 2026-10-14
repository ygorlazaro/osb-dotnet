// IPv4 / ICMP / ARP interface state and shared helpers.
//
// The interface gets its address from DHCP (Network/Dhcp.cs) and falls back
// to a fixed one only if no server answers. Address fields are therefore
// mutable: a lease replaces the defaults and everything downstream reads them
// at use time rather than caching them.
//
// Split across partials by concern:
//   NetStack.cs     configuration, buffers, frame parsing, checksum helpers
//   NetStack.Ip.cs  IPv4/UDP/ICMP transmit paths
//   NetStack.Arp.cs address resolution

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Network;

internal static unsafe partial class NetStack
{
    // ---- interface configuration ----
    // Defaults used until DHCP succeeds. 10.0.2.15 in hex: note 15 decimal
    // is 0x0F, not 0x15. These match QEMU's user-mode network.
    public const uint DefaultIp = 0x0A00020F;
    public const uint DefaultNetmask = 0xFFFFFF00;
    public const uint DefaultGateway = 0x0A000202;
    public const uint DefaultDns = 0x0A000203;

    public const uint Broadcast = 0xFFFFFFFF;
    public const ushort EtherIp = 0x0800;
    public const ushort EtherArp = 0x0806;

    public static uint Ip = DefaultIp;
    public static uint Netmask = DefaultNetmask;
    public static uint Gateway = DefaultGateway;
    public static uint Dns = DefaultDns;

    // How the current addressing was obtained, for NET to report.
    public static bool FromDhcp;
    public static uint DhcpServer;

    public const int FrameCap = 2048;

    private static byte* _tx;        // frame under construction (ICMP)
    private static byte* _udpTx;     // frame under construction (UDP/DHCP)
    private static byte* _arp;       // ARP must not clobber a frame being built
    private static byte* _rx;
    private static bool _up;
    private static ushort _ipId;

    public static bool Up => _up;

    // Probe the NIC and allocate the transmit/receive scratch buffers.
    public static bool Init()
    {
        if (!E1000.Init()) return false;
        _tx = AllocFrameBuf();
        _udpTx = AllocFrameBuf();
        _arp = AllocFrameBuf();
        _rx = AllocFrameBuf();
        if ((ulong)_tx == 0 || (ulong)_udpTx == 0 || (ulong)_arp == 0 || (ulong)_rx == 0) return false;
        _up = true;
        return true;
    }

    private static byte* AllocFrameBuf()
    {
        return (byte*)PhysicalMemory.Alloc(FrameCap / 4096 + 1, 4096);
    }

    // Apply a lease. The ARP cache is dropped because the cached entry was
    // learned while a different address was in force.
    public static void Configure(uint ip, uint mask, uint gateway, uint dns, bool viaDhcp)
    {
        Ip = ip; Netmask = mask; Gateway = gateway; Dns = dns;
        FromDhcp = viaDhcp;
        ForgetArp();
    }

    public static bool SameSubnet(uint a, uint b)
    {
        return (a & Netmask) == (b & Netmask);
    }

    // ---- byte helpers ----

    public static void WriteMac(byte* p, ulong mac)
    {
        for (int i = 0; i < 6; i++) p[i] = (byte)(mac >> (8 * i));
    }

    public static ulong ReadMac(byte* p)
    {
        ulong m = 0;
        for (int i = 0; i < 6; i++) m |= (ulong)p[i] << (8 * i);
        return m;
    }

    public static ushort ReadBe16(byte* p) { return (ushort)((p[0] << 8) | p[1]); }
    public static void WriteBe16(byte* p, uint v) { p[0] = (byte)(v >> 8); p[1] = (byte)v; }

    public static uint ReadBe32(byte* p)
    {
        return ((uint)p[0] << 24) | ((uint)p[1] << 16) | ((uint)p[2] << 8) | p[3];
    }

    public static void WriteBe32(byte* p, uint v)
    {
        p[0] = (byte)(v >> 24); p[1] = (byte)(v >> 16); p[2] = (byte)(v >> 8); p[3] = (byte)v;
    }

    public static void WriteLe16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }

    // Standard internet checksum over an even byte count. An odd trailing byte
    // is padded on the right, as the specification requires.
    public static uint Checksum(byte* data, int len)
    {
        return (~Carry(PartialSum(data, len, 0))) & 0xFFFF;
    }

    // Accumulates 16-bit words into a running sum, for the UDP pseudo-header.
    // Not folded: folding only happens once, at the end.
    public static uint PartialSum(byte* data, int len, uint sum)
    {
        int i = 0;
        for (; i + 1 < len; i += 2) sum += ReadBe16(data + i);
        if ((len & 1) != 0) sum += (uint)data[i] << 8;
        return sum;
    }

    // Folds the carries out of the high half. The sum must be folded BEFORE
    // it is complemented: complementing first and folding afterwards is not
    // the same thing once a carry exists, and produces a wrong checksum for
    // exactly the longer packets that carry.
    public static uint Carry(uint sum)
    {
        while ((sum >> 16) != 0) sum = (sum & 0xFFFF) + (sum >> 16);
        return sum;
    }

    // ---- receive side ----

    // Pull the next frame off the card. Returns its length, or 0.
    public static int Pump() { return E1000.Receive(_rx, FrameCap); }

    public static byte* RxBuffer { get { return _rx; } }

    // Routes one received frame. Echo replies are reported through
    // fromIp/seq; everything else is handled and discarded.
    public static bool Dispatch(bool answerEcho, out uint fromIp, out ushort seq)
    {
        fromIp = 0; seq = 0;
        int len = Pump();
        if (len < 14) return false;

        byte* f = _rx;
        ushort type = ReadBe16(f + 12);
        if (type == EtherArp) { HandleArp(f, len); return false; }
        if (type != EtherIp) return false;
        if (len < 14 + 20) return false;

        // IPv4 header offsets: 12 is the source address, 16 the destination.
        // Filtering on the source silently drops every reply, so this must be
        // the destination.
        byte* ip = f + 14;
        if ((ip[0] >> 4) != 4) return false;
        if (ip[9] != 1) return false;                  // ICMP only
        if (ReadBe32(ip + 16) != Ip) return false;     // not addressed to us

        byte* icmp = ip + 20;
        int icmpLen = len - 14 - 20;
        if (icmpLen < 8) return false;

        if (icmp[0] == 0)                              // echo reply
        {
            fromIp = ReadBe32(ip + 12);
            seq = ReadBe16(icmp + 6);
            return true;
        }
        if (icmp[0] == 8 && answerEcho) ReplyEcho(f, len, ip, icmp, icmpLen);
        return false;
    }

    // Turn an inbound echo request into a reply, in place.
    private static void ReplyEcho(byte* f, int len, byte* ip, byte* icmp, int icmpLen)
    {
        uint from = ReadBe32(ip + 12);                 // the requester's address
        icmp[0] = 0; icmp[1] = 0;
        WriteBe16(icmp + 2, 0);
        WriteBe16(icmp + 2, Checksum(icmp, icmpLen));
        WriteBe32(ip + 16, Ip);                        // we are now the source
        WriteBe32(ip + 12, from);
        WriteBe16(ip + 10, 0);
        WriteBe16(ip + 10, Checksum(ip, 20));

        ulong dst = ReadMac(f);                        // requester's MAC
        WriteMac(f, dst);
        WriteMac(f + 6, E1000.Mac);
        E1000.Send(f, len);
    }

    // Next IP identification field. Zero is legal but pointless; a counter
    // keeps fragments from a previous conversation looking like ours.
    public static ushort NextIpId() { _ipId++; return _ipId; }
}