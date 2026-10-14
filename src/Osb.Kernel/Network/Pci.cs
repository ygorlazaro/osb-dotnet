// PCI configuration-space access and bus enumeration.
//
// Legacy port I/O only (0xCF8 address, 0xCFC data), which is what QEMU and
// every real machine expose. The scan walks bus 0..255 / device 0..31 /
// function 0..7, following the multifunction bit in header type.
//
// Used by the network stack to find the NIC and by LOAD DRIVER so a driver
// can locate and claim its own hardware.

using Osb.Kernel.Hardware;

namespace Osb.Kernel.Network;

internal static unsafe class Pci
{
    private const ushort PortAddress = 0xCF8;
    private const ushort PortData = 0xCFC;

    private const int VendorIntel = 0x8086;

    // 32-bit register in the configuration header.
    public const int RegVendor = 0x00;      // vendor(16) | device(16)
    public const int RegCommand = 0x04;
    public const int RegStatus = 0x06;
    public const int RegRevision = 0x08;
    public const int RegClass = 0x09;       // class(8) | subclass(8) | progif(8)
    public const int RegHeaderType = 0x0E;
    public const int RegBar0 = 0x10;
    public const int RegInterruptLine = 0x3C;
    public const int RegInterruptPin = 0x3D;

    public const uint StatusInterrupt = 0x08;   // legacy INTx, bit0 of byte 3

    public const int BarIoSpace = 0x1;          // BAR bit0: 1 = I/O port

    // ---- config space access ----

    // Build the 0xCF8 address for a (bus, device, function, register).
    private static uint MakeAddress(int bus, int slot, int func, int reg)
    {
        return 0x80000000u
             | ((uint)bus << 16)
             | ((uint)(slot & 0x1F) << 11)
             | ((uint)(func & 0x07) << 8)
             | ((uint)reg & 0xFC);
    }

    public static void Write32(int bus, int slot, int func, int reg, uint value)
    {
        NativeMethods.outl(PortAddress, MakeAddress(bus, slot, func, reg));
        NativeMethods.outl(PortData, value);
    }

    public static uint Read32(int bus, int slot, int func, int reg)
    {
        NativeMethods.outl(PortAddress, MakeAddress(bus, slot, func, reg));
        return NativeMethods.inl(PortData);
    }

    public static ushort Read16(int bus, int slot, int func, int reg)
    {
        return (ushort)(Read32(bus, slot, func, reg) >> ((reg & 2) * 8));
    }

    public static byte Read8(int bus, int slot, int func, int reg)
    {
        return (byte)(Read32(bus, slot, func, reg) >> ((reg & 3) * 8));
    }

    public static ushort VendorId(int bus, int slot, int func) { return Read16(bus, slot, func, RegVendor); }
    public static ushort DeviceId(int bus, int slot, int func) { return Read16(bus, slot, func, RegVendor + 2); }

    // Enable bus mastering + memory space + I/O space (command register).
    public static void EnableBusMaster(int bus, int slot, int func)
    {
        ushort cmd = Read16(bus, slot, func, RegCommand);
        cmd |= 0x0007;                          // I/O + memory + bus master
        Write32(bus, slot, func, RegCommand, (uint)(cmd | (Read16(bus, slot, func, RegCommand) & 0xFF00)));
    }

    // ---- enumeration ----

    // Finds the first device matching a vendor/device id.
    public static bool Find(int vendor, int device, out int bus, out int slot, out int func)
    {
        for (int b = 0; b < 256; b++)
            for (int s = 0; s < 32; s++)
            {
                int maxFunc = (Read8(b, s, 0, 0x0E) & 0x80) != 0 ? 8 : 1;
                for (int f = 0; f < maxFunc; f++)
                {
                    if (VendorId(b, s, f) == 0xFFFF) continue;   // no device
                    if (VendorId(b, s, f) == (ushort)vendor && DeviceId(b, s, f) == (ushort)device)
                    {
                        bus = b; slot = s; func = f;
                        return true;
                    }
                }
            }
        bus = 0; slot = 0; func = 0;
        return false;
    }

    // Finds any device of a class/subclass (e.g. class 2 = network).
    public static bool FindClass(byte cls, byte sub, out int bus, out int slot, out int func)
    {
        for (int b = 0; b < 256; b++)
            for (int s = 0; s < 32; s++)
            {
                int maxFunc = (Read8(b, s, 0, 0x0E) & 0x80) != 0 ? 8 : 1;
                for (int f = 0; f < maxFunc; f++)
                {
                    if (VendorId(b, s, f) == 0xFFFF) continue;
                    uint classReg = Read32(b, s, f, RegClass);
                    byte c = (byte)(classReg >> 24);
                    byte sc = (byte)(classReg >> 16);
                    if (c == cls && sc == sub) { bus = b; slot = s; func = f; return true; }
                }
            }
        bus = 0; slot = 0; func = 0;
        return false;
    }

    // Decodes a base address register into either an I/O port or a
    // physical address. Returns false for an unimplemented BAR.
    public static bool DecodeBar(int bus, int slot, int func, int barIndex, out ulong baseAddr)
    {
        uint raw = Read32(bus, slot, func, RegBar0 + barIndex * 4);
        if (raw == 0) { baseAddr = 0; return false; }
        if ((raw & 1) != 0)                       // I/O space
        {
            baseAddr = raw & 0xFFFFFFF0u;
            return true;
        }
        baseAddr = raw & 0xFFFFFFF0u;             // memory: low 4 bits are flags
        return true;
    }

    public static byte InterruptLine(int bus, int slot, int func) { return Read8(bus, slot, func, RegInterruptLine); }
    public static byte InterruptPin(int bus, int slot, int func) { return Read8(bus, slot, func, RegInterruptPin); }
}
