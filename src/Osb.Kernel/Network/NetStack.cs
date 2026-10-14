// Ethernet / ARP / IPv4 / ICMP over the e1000 driver.
//
// Just enough of the four protocols for an ICMP echo: build and parse
// Ethernet frames, resolve a MAC with ARP, and wrap ICMP echo requests and
// replies in IPv4. The interface uses a fixed static address so there is no
// DHCP dependency.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Network;

internal static unsafe class NetStack
{
    // ---- static interface configuration (no DHCP) ----
    public const uint Ip = 0x0A000215;      // 10.0.2.15 (QEMU user-net)
    public const uint Netmask = 0xFFFFFF00; // /24
    public const uint Gateway = 0x0A000202; // 10.0.2.2
    public const ushort EtherIp = 0x0800;
    public const ushort EtherArp = 0x0806;

    private const int FrameCap = 2048;
    private static byte* _tx;
    private static byte* _rx;
    private static bool _up;

    public static bool Up => _up;

    // Bring the interface up (probe the NIC, seed the ARP table with the
    // gateway so ICMP to a LAN address never needs an ARP round-trip).
    public static bool Init()
    {
        if (!E1000.Init()) return false;
        _tx = (byte*)PhysicalMemory.Alloc(FrameCap / 4096 + 1, 4096);
        _rx = (byte*)PhysicalMemory.Alloc(FrameCap / 4096 + 1, 4096);
        if ((ulong)_tx == 0 || (ulong)_rx == 0) return false;
        _up = true;
        return true;
    }

    // ---- helpers ----
    private static void WriteMac(byte* p, ulong mac)
    {
        for (int i = 0; i < 6; i++) p[i] = (byte)(mac >> (8 * i));
    }
    private static ulong ReadMac(byte* p)
    {
        ulong m = 0;
        for (int i = 0; i < 6; i++) m |= (ulong)p[i] << (8 * i);
        return m;
    }
    private static ushort ReadBe16(byte* p) { return (ushort)((p[0] << 8) | p[1]); }
    private static void WriteBe16(byte* p, uint v) { p[0] = (byte)(v >> 8); p[1] = (byte)v; }
    private static uint ReadBe32(byte* p)
    {
        return ((uint)p[0] << 24) | ((uint)p[1] << 16) | ((uint)p[2] << 8) | p[3];
    }
    private static void WriteBe32(byte* p, uint v)
    {
        p[0] = (byte)(v >> 24); p[1] = (byte)(v >> 16); p[2] = (byte)(v >> 8); p[3] = (byte)v;
    }
    public static uint Checksum(byte* data, int len)
    {
        uint sum = 0;
        for (int i = 0; i + 1 < len; i += 2)
            sum += ReadBe16(data + i);
        if ((len & 1) != 0) sum += (uint)data[len - 1] << 8;
        while (sum >> 16 != 0) sum = (sum & 0xFFFF) + (sum >> 16);
        return (~sum) & 0xFFFF;
    }
    public static bool SameSubnet(uint a, uint b)
    {
        return (a & Netmask) == (b & Netmask);
    }

    // ---- transmit an ICMP echo to a destination IP ----
    public static bool SendPing(uint dstIp, ushort seq)
    {
        if (!_up) return false;
        int payload = 8;                  // ICMP echo: type, code, csum, id, seq
        int icmpLen = 8 + payload;
        int ipLen = 20 + icmpLen;
        int frameLen = 14 + ipLen;
        if (frameLen > FrameCap) return false;

        byte* f = _tx;
        WriteMac(f, E1000.Mac);                        // dst (resolved below)
        WriteMac(f + 6, E1000.Mac);                    // src
        WriteBe16(f + 12, EtherIp);

        byte* ip = f + 14;
        ip[0] = 0x45;                                   // version 4, IHL 5
        ip[1] = 0;                                      // tos
        WriteBe16(ip + 2, (ushort)ipLen);               // total length
        WriteBe16(ip + 4, 0);                           // id
        WriteBe16(ip + 6, 0x4000);                      // flags: don't fragment
        ip[8] = 64;                                     // ttl
        ip[9] = 1;                                      // protocol ICMP
        WriteBe16(ip + 10, 0);                          // checksum placeholder
        WriteBe32(ip + 12, Ip);                         // src
        WriteBe32(ip + 16, dstIp);                      // dst
        WriteBe16(ip + 10, Checksum(ip, 20));

        byte* icmp = ip + 20;
        icmp[0] = 8; icmp[1] = 0;                       // echo request
        WriteBe16(icmp + 2, 0);                         // checksum placeholder
        WriteBe16(icmp + 4, (ushort)0x1234);            // id
        WriteBe16(icmp + 6, seq);                       // seq
        for (int i = 0; i < payload; i++) icmp[8 + i] = (byte)('a' + (i % 26));
        WriteBe16(icmp + 2, Checksum(icmp, icmpLen));

        // resolve the destination MAC: send directly on-LAN, else via gateway
        uint next = SameSubnet(Ip, dstIp) ? dstIp : Gateway;
        ulong dstMac = ArpResolve(next);
        if (dstMac == 0) return false;
        WriteMac(f, dstMac);
        WriteMac(f + 6, E1000.Mac);

        return E1000.Send(f, frameLen);
    }

    // ---- receive loop: answer ICMP echoes, learn ARP ----
    // Returns true when an echo reply to one of our pings was seen.
    public static bool Poll(bool answerEcho, out uint fromIp, out ushort seq)
    {
        fromIp = 0; seq = 0;
        if (!_up) return false;
        int len = E1000.Receive(_rx, FrameCap);
        if (len < 14) return false;

        byte* f = _rx;
        ushort type = ReadBe16(f + 12);
        if (type == EtherArp) { HandleArp(f, len); return false; }
        if (type != EtherIp) return false;
        if (len < 14 + 20) return false;

        byte* ip = f + 14;
        if ((ip[0] >> 4) != 4) return false;
        if (ip[9] != 1) return false;                  // ICMP only
        if (ReadBe32(ip + 12) != Ip) return false;     // not for us

        byte* icmp = ip + 20;
        int icmpLen = len - 14 - 20;
        if (icmpLen < 8) return false;

        if (icmp[0] == 0)                              // echo reply
        {
            fromIp = ReadBe32(ip + 16);
            seq = ReadBe16(icmp + 6);
            return true;
        }
        if (icmp[0] == 8 && answerEcho)                // echo request -> reply
        {
            ReplyEcho(f, len, ip, icmp, icmpLen);
        }
        return false;
    }

    private static void ReplyEcho(byte* f, int len, byte* ip, byte* icmp, int icmpLen)
    {
        uint from = ReadBe32(ip + 16);
        // turn the request into a reply
        icmp[0] = 0; icmp[1] = 0;
        WriteBe16(icmp + 2, 0);
        WriteBe16(icmp + 2, Checksum(icmp, icmpLen));
        WriteBe32(ip + 16, Ip);
        WriteBe32(ip + 12, from);
        WriteBe16(ip + 10, 0);
        WriteBe16(ip + 10, Checksum(ip, 20));

        ulong dst = ReadMac(f);                          // requester MAC
        WriteMac(f, dst);
        WriteMac(f + 6, E1000.Mac);
        E1000.Send(f, len);
    }

    // ---- ARP (single static cache entry for the gateway) ----
    private static ulong _arpMac;
    private static uint _arpIp;

    private static ulong ArpResolve(uint ip)
    {
        if (_arpIp == ip && _arpMac != 0) return _arpMac;
        // broadcast "who has ip?"
        int frameLen = 14 + 28;
        byte* f = _tx;
        for (int i = 0; i < 6; i++) f[i] = 0xFF;
        WriteMac(f + 6, E1000.Mac);
        WriteBe16(f + 12, EtherArp);
        byte* a = f + 14;
        WriteBe16(a, (ushort)1); WriteBe16(a + 2, (ushort)0x0800);  // hw=ethernet, proto=ip
        a[4] = 6; a[5] = 4; WriteBe16(a + 6, (ushort)1);            // hlen, plen, op=request
        WriteMac(a + 8, E1000.Mac);
        WriteBe32(a + 14, Ip);
        WriteBe32(a + 18, ip);
        E1000.Send(f, frameLen);

        // collect replies for a moment
        for (int i = 0; i < 2000; i++)
        {
            int rl = E1000.Receive(_rx, FrameCap);
            if (rl < 42) continue;
            if (ReadBe16(_rx + 12) != EtherArp) continue;
            byte* ra = _rx + 14;
            if (ReadBe16(ra + 6) != (ushort)2) continue;         // reply
            if (ReadBe32(ra + 14) != ip) continue;
            ulong mac = ReadMac(ra + 8);
            _arpIp = ip; _arpMac = mac;
            return mac;
        }
        return 0;
    }

    private static void HandleArp(byte* f, int len)
    {
        if (len < 42) return;
        byte* a = f + 14;
        if (ReadBe16(a + 6) != (ushort)1) return;       // request
        if (ReadBe32(a + 14) != Ip) return;             // not for us

        // Save the requester's identity before the reply overwrites those fields.
        ulong reqMac = ReadMac(f);                      // ethernet source == sender MAC
        uint reqIp = ReadBe32(a + 14);

        WriteBe16(a + 6, (ushort)2);                            // operation: reply
        WriteMac(a + 8, E1000.Mac);                     // sender MAC = us
        WriteBe32(a + 14, Ip);                          // sender IP = us
        WriteMac(a + 18, reqMac);                       // target MAC = requester
        WriteBe32(a + 24, reqIp);                       // target IP = requester

        WriteMac(f, reqMac);                            // ethernet dest = requester
        WriteMac(f + 6, E1000.Mac);
        E1000.Send(f, 42);
    }
}
