// ATA PIO (IDE) driver: primary channel, LBA28, one 512-byte sector per call.
// Ext4.cs is the only user; it turns its 4 KiB blocks into the eight sectors
// this driver transfers.

using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Storage;

internal static unsafe class Ata
{
    private const int SectorWords = 512 / 2;   // 256 x 16-bit words

    // Spin while the controller reports BSY.
    public static void WaitBusy()
    {
        while ((NativeMethods.inb(Layout.AtaPrimaryStatus) & Layout.AtaStatusBusy) != 0) { }
    }

    // Spin until the drive raises DRQ (data ready to transfer).
    public static void WaitDrq()
    {
        while ((NativeMethods.inb(Layout.AtaPrimaryStatus) & Layout.AtaStatusDrq) == 0) { }
    }

    // Point the primary master at `lba` and issue `command`, then wait for the
    // data phase to open.
    private static void Select(ulong lba, byte command)
    {
        WaitBusy();
        NativeMethods.outb(Layout.AtaPrimaryDriveHead, (byte)(0xE0 | (byte)((lba >> 24) & 0x0F)));
        NativeMethods.outb(Layout.AtaPrimarySecCount, 1);
        NativeMethods.outb(Layout.AtaPrimaryLbaLow, (byte)(lba & 0xFF));
        NativeMethods.outb(Layout.AtaPrimaryLbaMid, (byte)((lba >> 8) & 0xFF));
        NativeMethods.outb(Layout.AtaPrimaryLbaHigh, (byte)((lba >> 16) & 0xFF));
        NativeMethods.outb(Layout.AtaPrimaryCommand, command);
        WaitBusy();
        WaitDrq();
    }

    public static void ReadSector(ulong lba, byte* buffer)
    {
        Select(lba, Layout.AtaCmdReadSectors);
        for (int i = 0; i < SectorWords; i++)
            ((ushort*)buffer)[i] = NativeMethods.inw(Layout.AtaPrimaryData);
    }

    public static void WriteSector(ulong lba, byte* buffer)
    {
        Select(lba, Layout.AtaCmdWriteSectors);
        for (int i = 0; i < SectorWords; i++)
            NativeMethods.outw(Layout.AtaPrimaryData, ((ushort*)buffer)[i]);
        WaitBusy();
    }
}