// PCI: list the bus and show where the network hardware lives.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class PciCommand
{
    // PCI <scan>  - walk the bus and list every device
    // PCI         - same as PCI <scan>
    public static void Run(char* line, int len)
    {
        int i = 3;
        while (i < len && line[i] == ' ') i++;
        if (i >= len || !Osb.Kernel.Shell.ShellText.StartsWithAt(line, len, i, "scan"))
        {
            List();
            return;
        }
        List();
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
