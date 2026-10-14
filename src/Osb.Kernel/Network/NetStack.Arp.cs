// Address resolution.
//
// One cache entry, which is all a polled client needs: the gateway's MAC
// carries every off-link destination, and on-link peers are rare enough that
// resolving on demand is fine. DHCP seeds this entry directly, because the
// server identifies itself in the packet it sends.

namespace Osb.Kernel.Network;

internal static unsafe partial class NetStack
{
    private static ulong _arpMac;
    private static uint _arpIp;

    public static void ForgetArp() { _arpMac = 0; _arpIp = 0; }

    // Record a MAC learned out of band (DHCP knows the server's).
    public static void LearnMac(uint ip, ulong mac)
    {
        _arpIp = ip; _arpMac = mac;
    }

    public static bool TryCachedMac(uint ip, out ulong mac)
    {
        if (_arpIp == ip && _arpMac != 0) { mac = _arpMac; return true; }
        mac = 0;
        return false;
    }

    // Resolve an address to a MAC, asking on the wire if it is not cached.
    public static ulong ArpResolve(uint ip)
    {
        ulong cached;
        if (TryCachedMac(ip, out cached)) return cached;

        byte* f = _arp;
        for (int i = 0; i < 14 + 28; i++) f[i] = 0;
        for (int i = 0; i < 6; i++) f[i] = 0xFF;        // broadcast
        WriteMac(f + 6, E1000.Mac);
        WriteBe16(f + 12, EtherArp);

        // ARP payload offsets: 0 hardware type, 2 protocol type, 4 hardware
        // length, 5 protocol length, 6 opcode, 8 sender MAC, 14 sender IP,
        // 18 target MAC, 24 target IP.
        byte* a = f + 14;
        WriteBe16(a, 1); WriteBe16(a + 2, EtherIp);
        a[4] = 6; a[5] = 4; WriteBe16(a + 6, 1);        // lengths, opcode: request
        WriteMac(a + 8, E1000.Mac);
        WriteBe32(a + 14, Ip);
        WriteBe32(a + 24, ip);                          // target MAC stays zero
        E1000.Send(f, 14 + 28);

        // Enabling receive arms a one-second coalescing delay in the card, so
        // the window is measured in ticks rather than iterations. The loop halts
        // rather than spins: busy-polling burns the whole window in a fraction
        // of the time it nominally waits for. Bounded by iteration count as a
        // backstop in case the timer stops.
        ulong deadline = Osb.Kernel.Cpu.Pit.Ticks + 2UL * (ulong)Osb.Kernel.Cpu.Pit.Hz;
        for (int i = 0; i < 4096; i++)
        {
            if (ArpReply(ip, out ulong found)) return found;
            if (Osb.Kernel.Cpu.Pit.Ticks >= deadline) break;
            Osb.Kernel.Hardware.NativeMethods.halt();
        }
        return 0;
    }

    // Consume one received frame looking for an ARP reply to `ip`.
    private static bool ArpReply(uint ip, out ulong mac)
    {
        mac = 0;
        int len = Pump();
        if (len < 42) return false;
        byte* f = RxBuffer;
        if (ReadBe16(f + 12) != EtherArp) return false;
        byte* a = f + 14;
        if (ReadBe16(a + 6) != 2) return false;          // opcode: reply
        if (ReadBe32(a + 14) != ip) return false;        // sender is who we asked
        mac = ReadMac(a + 8);
        if (mac == 0) return false;
        _arpIp = ip; _arpMac = mac;
        return true;
    }

    // Answer an ARP request for our own address.
    private static void HandleArp(byte* f, int len)
    {
        if (len < 42) return;
        byte* a = f + 14;
        if (ReadBe16(a + 6) != 1) return;               // opcode: request
        if (ReadBe32(a + 24) != Ip) return;             // target is someone else

        // The requester's identity has to be read before the reply overwrites it.
        ulong reqMac = ReadMac(f);
        uint reqIp = ReadBe32(a + 14);

        WriteBe16(a + 6, 2);                            // opcode: reply
        WriteMac(a + 8, E1000.Mac);
        WriteBe32(a + 14, Ip);
        WriteMac(a + 18, reqMac);
        WriteBe32(a + 24, reqIp);

        WriteMac(f, reqMac);
        WriteMac(f + 6, E1000.Mac);
        E1000.Send(f, 42);
    }
}