// PCI: list the bus and show where the network hardware lives.
//
// PCI          list every device on the bus
// PCI <addr>   show one device in detail, by B:D.F (e.g. 0:3.0)
// PCI net      show the network adapter, its driver state and addressing

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class PciCommand
{
    // Row labels for the network view. Written as char arrays because this is
    // freestanding code with no managed string literals for formatting; each
    // label is exactly 13 characters.
    private const string LAddress = "  address    ";
    private const string LGateway = "  gateway    ";
    private const string LNetmask = "  netmask    ";

    public static void Run(char* line, int len)
    {
        int i = 3;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { List(); return; }

        if (IsWord(line, len, i, 'n', 'e', 't')) { Net(); return; }
        if (IsWord(line, len, i, 's', 'c', 'a', 'n')) { List(); return; }

        int bus, slot, func;
        if (ParseBdf(line, len, i, out bus, out slot, out func))
        {
            Detail(bus, slot, func);
            return;
        }
        List();
    }

    // Matches a bare word given as individual characters, so no managed
    // string literal is needed in this freestanding code.
    private static bool IsWord(char* line, int len, int start, char a, char b, char c)
    {
        if (start + 3 > len) return false;
        if (line[start] != a || line[start + 1] != b || line[start + 2] != c) return false;
        return start + 3 == len || line[start + 3] == ' ';
    }

    private static bool IsWord(char* line, int len, int start, char a, char b, char c, char d)
    {
        if (start + 4 > len) return false;
        if (line[start] != a || line[start + 1] != b || line[start + 2] != c || line[start + 3] != d) return false;
        return start + 4 == len || line[start + 4] == ' ';
    }

    // Parses "B:D.F" or "B:D" at `start`.
    private static bool ParseBdf(char* p, int len, int start, out int bus, out int slot, out int func)
    {
        bus = 0; slot = 0; func = 0;
        int i = start;
        int v = 0; bool any = false;
        while (i < len && p[i] >= '0' && p[i] <= '9') { v = v * 10 + (p[i] - '0'); i++; any = true; }
        if (!any || i >= len || p[i] != ':') return false;
        bus = v; i++;
        v = 0; any = false;
        while (i < len && p[i] >= '0' && p[i] <= '9') { v = v * 10 + (p[i] - '0'); i++; any = true; }
        if (!any || slot > 31) { }
        slot = v;
        if (slot > 31) return false;
        if (i < len && p[i] == '.')
        {
            i++; v = 0; any = false;
            while (i < len && p[i] >= '0' && p[i] <= '9') { v = v * 10 + (p[i] - '0'); i++; any = true; }
            if (!any) return false;
            func = v;
        }
        return bus < 256 && slot < 32 && func < 8;
    }

    // One device in full: identity, class, BARs and interrupt line.
    private static void Detail(int b, int s, int f)
    {
        ushort ven = Osb.Kernel.Network.Pci.VendorId(b, s, f);
        if (ven == 0xFFFF) { Vga.WriteLine("No device there."); return; }
        ushort dev = Osb.Kernel.Network.Pci.DeviceId(b, s, f);
        uint classReg = Osb.Kernel.Network.Pci.Read32(b, s, f, Osb.Kernel.Network.Pci.RegClass);

        Vga.Write("bus "); Vga.WriteDec((ulong)b);
        Vga.Write(", device "); Vga.WriteDec((ulong)s);
        Vga.Write(", function "); Vga.WriteDec((ulong)f);
        Vga.PutChar('\n');

        Vga.Write("  id      "); Vga.WriteHex((ulong)ven);
        Vga.PutChar(':'); Vga.WriteHex((ulong)dev); Vga.PutChar('\n');
        Vga.Write("  class   "); Vga.WriteHex((ulong)(classReg >> 24));
        Vga.Write("  irq "); Vga.WriteDec((ulong)Osb.Kernel.Network.Pci.InterruptLine(b, s, f));
        Vga.PutChar('\n');

        for (int bar = 0; bar < 6; bar++)
        {
            ulong addr;
            if (!Osb.Kernel.Network.Pci.DecodeBar(b, s, f, bar, out addr)) continue;
            Vga.Write("  BAR"); Vga.WriteDec((ulong)bar);
            Vga.Write("   "); Vga.WriteHex(addr); Vga.PutChar('\n');
        }
    }

    // The network adapter: where it is, whether the driver bound it, and the
    // addressing in force. This is what you check first when PING misbehaves.
    private static void Net()
    {
        int b, s, f;
        if (!Osb.Kernel.Network.Pci.FindClass(2, 0, out b, out s, out f))
        {
            Vga.WriteLine("No network adapter found on the PCI bus.");
            return;
        }
        Vga.Write("adapter at ");
        Vga.WriteDec((ulong)b); Vga.PutChar(':');
        Vga.WriteDec((ulong)s); Vga.PutChar('.');
        Vga.WriteDec((ulong)f);
        Vga.Write("  id ");
        Vga.WriteHex((ulong)Osb.Kernel.Network.Pci.VendorId(b, s, f));
        Vga.PutChar(':'); Vga.WriteHex((ulong)Osb.Kernel.Network.Pci.DeviceId(b, s, f));
        Vga.PutChar('\n');

        ulong bar;
        if (Osb.Kernel.Network.Pci.DecodeBar(b, s, f, 0, out bar))
        {
            Vga.Write("  registers  ");
            Vga.WriteHex(bar);
            Vga.PutChar('\n');
        }

        if (!Osb.Kernel.Network.E1000.Ready)
        {
            Vga.Write("  driver     e1000, not started yet\n");
            Vga.WriteLine("              (run PING to bring the adapter up)");
            return;
        }

        byte* mac = stackalloc byte[18];
        Osb.Kernel.Network.E1000.FormatMac(mac, Osb.Kernel.Network.E1000.Mac);
        Vga.Write("  driver     e1000, bound, link up\n");
        Vga.Write("  rx ring    head ");
        Vga.WriteDec((ulong)Osb.Kernel.Network.E1000.RxHead);
        Vga.Write(" tail ");
        Vga.WriteDec((ulong)Osb.Kernel.Network.E1000.RxTail);
        Vga.PutChar('\n');
        Vga.Write("  mac        ");
        // One byte at a time: the formatted MAC is 17 bytes, but WriteStr
        // counts 16-bit characters and would read past the end of it.
        for (int i = 0; i < 17; i++) Vga.PutChar((char)mac[i]);
        Vga.PutChar('\n');

        Vga.Write("  speed      ");
        int speed = Osb.Kernel.Network.E1000.Speed;
        if (speed == 2) Vga.WriteLine("1000 Mbps");
        else if (speed == 1) Vga.WriteLine("100 Mbps");
        else Vga.WriteLine("10 Mbps");

        WriteAddr(LAddress, Osb.Kernel.Network.NetStack.Ip);
        WriteAddr(LGateway, Osb.Kernel.Network.NetStack.Gateway);
        WriteAddr(LNetmask, Osb.Kernel.Network.NetStack.Netmask);
    }

    // Dotted-quad from a packed address, with a trailing comment.
    // Writes one "label  a.b.c.d" row. The label already carries its newline,
    // so the caller supplies the address column only.
    // Each octet is passed already shifted down to the low byte.
    private static void WriteOctet(uint shifted) { Vga.WriteDec((ulong)(shifted & 0xFF)); }

    // One "label value" row. Label and value are written separately so the
    // columns line up regardless of how long the label is.
    private static void WriteAddr(string label, uint ip)
    {
        Vga.Write(label);
        WriteOctet(ip >> 24); Vga.PutChar('.');
        WriteOctet(ip >> 16); Vga.PutChar('.');
        WriteOctet(ip >> 8); Vga.PutChar('.');
        WriteOctet(ip); Vga.PutChar('\n');
    }

    private static void List()
    {
        int found = 0;
        for (int b = 0; b < 2; b++)
            for (int s = 0; s < 32; s++)
            {
                int maxFunc = (Osb.Kernel.Network.Pci.Read8(b, s, 0, 0x0E) & 0x80) != 0 ? 8 : 1;
                for (int f = 0; f < maxFunc; f++)
                {
                    ushort ven = Osb.Kernel.Network.Pci.VendorId(b, s, f);
                    if (ven == 0xFFFF) continue;
                    ushort dev = Osb.Kernel.Network.Pci.DeviceId(b, s, f);
                    uint classReg = Osb.Kernel.Network.Pci.Read32(b, s, f, Osb.Kernel.Network.Pci.RegClass);
                    found++;
                    Vga.Write("  ");
                    Vga.WriteHex((ulong)(uint)b); Vga.PutChar(':');
                    Vga.WriteHex((ulong)(uint)s); Vga.PutChar('.');
                    Vga.WriteHex((ulong)(uint)f);
                    Vga.Write("  ");
                    Vga.WriteHex((ulong)ven); Vga.PutChar(':');
                    Vga.WriteHex((ulong)dev);
                    Vga.Write("  class ");
                    Vga.WriteHex((ulong)(classReg >> 24));
                    Vga.Write(" irq ");
                    Vga.WriteDec((ulong)Osb.Kernel.Network.Pci.InterruptLine(b, s, f));
                    Vga.PutChar('\n');
                }
            }
        if (found == 0) Vga.WriteLine("No PCI devices found.");
        else
        {
            Vga.Write("  ");
            Vga.WriteDec((ulong)found);
            Vga.WriteLine(" device(s).");
        }
    }
}
