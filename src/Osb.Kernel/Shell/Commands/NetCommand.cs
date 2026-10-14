// NET / IFCONFIG - show or change the interface addressing.
//
// NET              prints the current configuration
// NET <ip>         assigns a static address (netmask and gateway follow the
//                  class of the address given)
// NET DHCP         asks a DHCP server for an address
// NET AUTO         DHCP, falling back to the built-in static address
//
// The shell deliberately does not configure the interface on its own: a
// bring-up that depends on a network server would stall the boot, and a
// serial console with no network is the normal case for a home OS.

using Osb.Kernel.Hardware;
using Osb.Kernel.Network;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class NetCommand
{
    public static void Run(char* line, int len)
    {
        int i = 3;                                       // past "NET"
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Show(); return; }

        if (IsWord(line, len, i, 'D', 'H', 'C', 'P')) { Renew(); return; }
        if (IsWord(line, len, i, 'A', 'U', 'T', 'O')) { Auto(); return; }

        uint ip;
        if (!ParseIp(line, i, len, out ip)) { Show(); return; }
        Assign(ip);
    }

    // Bare word match given as characters: this is freestanding code, so no
    // managed string literals for parsing.
    private static bool IsWord(char* line, int len, int start, char a, char b, char c, char d)
    {
        if (start + 4 > len) return false;
        if (line[start] != a || line[start + 1] != b || line[start + 2] != c || line[start + 3] != d) return false;
        return start + 4 == len || line[start + 4] == ' ';
    }

    // ---- operations ----

    private static void Show()
    {
        if (!E1000.Ready)
        {
            Vga.Write("adapter     not started yet (PCI net for hardware detail)\n");
        }
        else
        {
            byte* mac = stackalloc byte[18];
            E1000.FormatMac(mac, E1000.Mac);
            Vga.Write("adapter     e1000 ");
            for (int i = 0; i < 17; i++) Vga.PutChar((char)mac[i]);
            Vga.PutChar('\n');
        }

        Vga.Write("address     ");
        WriteIp(NetStack.Ip);
        Vga.Write("  netmask ");
        WriteIp(NetStack.Netmask);
        Vga.PutChar('\n');

        Vga.Write("gateway     ");
        WriteIp(NetStack.Gateway);
        Vga.Write("  dns ");
        WriteIp(NetStack.Dns);
        Vga.PutChar('\n');

        Vga.Write("source      ");
        Vga.WriteLine(NetStack.FromDhcp ? "DHCP lease" : "static (NET <ip> or NET DHCP to change)");
    }

    private static void Renew()
    {
        if (!NetStack.Up && !NetStack.Init())
        {
            Vga.WriteLine("DHCP: no network adapter (start one with PCI net).");
            return;
        }
        Vga.WriteLine("DHCP: requesting an address ...");
        if (!Dhcp.Run()) { ReportFailure(); return; }
        ReportSuccess();
    }

    private static void Auto()
    {
        if (!NetStack.Up && !NetStack.Init())
        {
            Vga.WriteLine("NET: no network adapter.");
            return;
        }

        // Remember what already works. A DHCP request can fail transiently,
        // and replacing a working lease with the built-in address because of
        // that would take a working network away from the user.
        bool hadAddress = NetStack.Up && NetStack.Ip != 0;
        uint keepIp = NetStack.Ip, keepMask = NetStack.Netmask, keepGw = NetStack.Gateway;
        bool keepWasDhcp = NetStack.FromDhcp;

        if (Dhcp.Run()) { ReportSuccess(); return; }
        ReportFailure();

        if (hadAddress)
        {
            NetStack.Configure(keepIp, keepMask, keepGw, NetStack.Dns, keepWasDhcp);
            Vga.WriteLine("keeping the addressing already in force.");
            Show();
            return;
        }

        // Nothing was configured before, so fall back rather than leaving the
        // interface with no address at all.
        NetStack.Configure(NetStack.DefaultIp, NetStack.DefaultNetmask,
                           NetStack.DefaultGateway, NetStack.DefaultDns, false);
        Vga.WriteLine("falling back to the built-in static address.");
        Show();
    }

    private static void Assign(uint ip)
    {
        // Classful defaults: a /24 for anything that is not in one of the
        // special ranges. Good enough for a home network, and visible.
        uint mask = NetStack.DefaultNetmask;
        uint gw = NetStack.Gateway;
        if (ip >= 0x0A000000 && ip <= 0x0AFFFFFF) { mask = 0xFF000000; gw = 0x0A000001; }
        else if (ip >= 0xC0A80000 && ip <= 0xC0A8FFFF) { mask = 0xFFFF0000; gw = 0xC0A80101; }
        else if (ip >= 0xAC100000 && ip <= 0xAC1FFFFF) { mask = 0xFFF00000; gw = 0xAC100001; }

        NetStack.Configure(ip, mask, gw, NetStack.Dns, false);
        Vga.Write("address set to ");
        WriteIp(ip);
        Vga.PutChar('\n');
    }

    private static void ReportSuccess()
    {
        Vga.WriteLine("DHCP: lease obtained.");
        Vga.Write("  address  ");
        WriteIp(NetStack.Ip);
        Vga.PutChar('\n');
        Vga.Write("  netmask  ");
        WriteIp(NetStack.Netmask);
        Vga.PutChar('\n');
        Vga.Write("  gateway  ");
        WriteIp(NetStack.Gateway);
        Vga.PutChar('\n');
        Vga.Write("  server   ");
        WriteIp(Dhcp.DhcpServer);
        Vga.PutChar('\n');
    }

    private static void ReportFailure()
    {
        Vga.Write("DHCP: no response from a server");
        int f = Dhcp.Fail;
        if (f == 1) Vga.Write(" (no offer)");
        else if (f == 2) Vga.Write(" (offer, no acknowledgement)");
        else if (f == 3) Vga.Write(" (out of memory)");
        Vga.PutChar('\n');
    }

    // ---- formatting ----

    private static void WriteIp(uint ip)
    {
        Vga.WriteDec((ulong)((ip >> 24) & 0xFF)); Vga.PutChar('.');
        Vga.WriteDec((ulong)((ip >> 16) & 0xFF)); Vga.PutChar('.');
        Vga.WriteDec((ulong)((ip >> 8) & 0xFF)); Vga.PutChar('.');
        Vga.WriteDec((ulong)(ip & 0xFF));
    }

    // Dotted quad only. Names need DNS, which is a later step.
    // Stops at the first character that cannot belong to an address, so
    // trailing spaces or extra words do not turn a valid address into an
    // error.
    private static bool ParseIp(char* p, int start, int len, out uint ip)
    {
        ip = 0;
        uint part = 0;
        int digits = 0;
        int octets = 0;
        int i = start;
        for (; i < len; i++)
        {
            char c = p[i];
            if (c == '.' || (c >= '0' && c <= '9'))
            {
                if (c == '.')
                {
                    if (digits == 0 || part > 255) return false;
                    ip = (ip << 8) | (part & 0xFF);
                    octets++;
                    part = 0; digits = 0;
                }
                else
                {
                    part = part * 10 + (uint)(c - '0');
                    if (++digits > 3) return false;
                }
                continue;
            }
            break;
        }
        if (digits == 0 || part > 255) return false;
        ip = (ip << 8) | (part & 0xFF);
        octets++;
        return octets == 4;
    }
}