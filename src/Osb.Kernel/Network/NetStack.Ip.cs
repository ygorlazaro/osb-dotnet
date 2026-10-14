// IPv4 and UDP transmit paths.
//
// Builds the headers that sit between the ethernet frame and the payload:
// IPv4 with a correct header checksum, and UDP with the pseudo-header
// checksum that UDP requires. DHCP uses the UDP path; ICMP is assembled
// directly because it needs no transport layer.

namespace Osb.Kernel.Network;

internal static unsafe partial class NetStack
{
    // Write an IPv4 header for `payloadLen` bytes of transport payload.
    // Returns a pointer to the transport header that follows it.
    private static byte* IpHeader(byte* f, uint src, uint dst, byte proto, int payloadLen)
    {
        WriteMac(f, 0);                                // destination MAC, set by caller
        WriteMac(f + 6, E1000.Mac);
        WriteBe16(f + 12, EtherIp);

        byte* ip = f + 14;
        int total = 20 + payloadLen;
        ip[0] = 0x45;                                  // version 4, header length 5 words
        ip[1] = 0;                                     // type of service
        WriteBe16(ip + 2, (ushort)total);              // total length
        WriteBe16(ip + 4, NextIpId());                 // identification
        WriteBe16(ip + 6, 0x4000);                     // don't fragment
        ip[8] = 64;                                    // time to live
        ip[9] = proto;
        WriteBe16(ip + 10, 0);                         // checksum placeholder
        WriteBe32(ip + 12, src);
        WriteBe32(ip + 16, dst);
        WriteBe16(ip + 10, Checksum(ip, 20));
        return ip + 20;
    }

    // Write a UDP header and stamp the checksum over the pseudo-header.
    // A zero checksum is legal for IPv4 but computing it properly costs
    // nothing and keeps strict receivers happy.
    private static void UdpHeader(byte* udp, uint srcIp, uint dstIp, ushort sport, ushort dport, int payloadLen)
    {
        int udpLen = 8 + payloadLen;
        WriteBe16(udp, sport);
        WriteBe16(udp + 2, dport);
        WriteBe16(udp + 4, (ushort)udpLen);
        WriteBe16(udp + 6, 0);                         // checksum placeholder

        // Pseudo-header: src, dst, zero, protocol, UDP length.
        byte* ph = stackalloc byte[12];
        WriteBe32(ph, srcIp);
        WriteBe32(ph + 4, dstIp);
        ph[8] = 0; ph[9] = 17;                         // protocol: UDP
        WriteBe16(ph + 10, (ushort)udpLen);
        uint sum = PartialSum(ph, 12, 0);
        sum = PartialSum(udp, udpLen, sum);
        ushort csum = (ushort)((~Carry(sum)) & 0xFFFF);
        // Zero means "no checksum" on the wire, so send 0xFFFF instead.
        WriteBe16(udp + 6, csum == 0 ? (ushort)0xFFFF : csum);
    }

    // Send a UDP datagram in one ethernet frame. Broadcast destination is
    // selected by passing Broadcast as the address, which is what DHCP needs
    // before it has an address of its own.
    public static bool SendUdp(uint srcIp, uint dstIp, ushort sport, ushort dport,
                               byte* payload, int payloadLen)
    {
        if (!_up) return false;
        int frameLen = 14 + 20 + 8 + payloadLen;
        if (frameLen > FrameCap) return false;

        byte* f = _udpTx;
        byte* udp = IpHeader(f, srcIp, dstIp, 17, 8 + payloadLen);
        // The payload must be in place BEFORE the checksum is computed: UDP
        // covers the whole datagram, and summing a not-yet-copied buffer yields
        // a checksum over whatever was left there before. Receivers drop that
        // silently, which looks exactly like the message never arriving.
        for (int i = 0; i < payloadLen; i++) udp[8 + i] = payload[i];
        UdpHeader(udp, srcIp, dstIp, sport, dport, payloadLen);

        bool broadcast = dstIp == Broadcast;
        ulong dstMac = broadcast ? 0x010505050505UL : ArpResolve(dstIp);
        if (!broadcast && dstMac == 0) return false;
        if (broadcast) for (int i = 0; i < 6; i++) f[i] = 0xFF;
        else WriteMac(f, dstMac);
        return E1000.Send(f, frameLen);
    }

    // Send an ICMP echo request. Returns false when the destination cannot be
    // reached at layer two.
    public static bool SendPing(uint dstIp, ushort seq)
    {
        if (!_up) return false;
        const int payload = 8;                          // payload after the 8-byte header
        int icmpLen = 8 + payload;
        int frameLen = 14 + 20 + icmpLen;
        if (frameLen > FrameCap) return false;

        byte* f = _tx;
        byte* icmp = IpHeader(f, Ip, dstIp, 1, icmpLen);   // protocol 1 = ICMP

        icmp[0] = 8; icmp[1] = 0;                       // echo request
        WriteBe16(icmp + 2, 0);                         // checksum placeholder
        WriteBe16(icmp + 4, (ushort)0x4F53);            // identification
        WriteBe16(icmp + 6, seq);                       // sequence
        for (int i = 0; i < payload; i++) icmp[8 + i] = (byte)('a' + (i % 26));
        WriteBe16(icmp + 2, Checksum(icmp, icmpLen));

        // On-link targets are addressed directly, anything else via the gateway.
        uint next = SameSubnet(Ip, dstIp) ? dstIp : Gateway;
        ulong dstMac = ArpResolve(next);
        if (dstMac == 0) return false;
        WriteMac(f, dstMac);
        return E1000.Send(f, frameLen);
    }
}