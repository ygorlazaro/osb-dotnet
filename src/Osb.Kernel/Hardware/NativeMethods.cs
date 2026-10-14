using System;
using System.Runtime.InteropServices;

namespace Osb.Kernel.Hardware;

internal static unsafe partial class NativeMethods
{
    // ---- hardware access (implemented in boot.S / interrupts.S) ----
    [DllImport("osbnative")] public static extern void outb(ushort port, byte value);
    [DllImport("osbnative")] public static extern void outw(ushort port, ushort value);
    [DllImport("osbnative")] public static extern byte inb(ushort port);
    [DllImport("osbnative")] public static extern ushort inw(ushort port);
    [DllImport("osbnative")] public static extern void lidt(byte* idtr);
    [DllImport("osbnative")] public static extern ulong osb_get_stub_table();
    [DllImport("osbnative")] public static extern void sti();
    [DllImport("osbnative")] public static extern void cli();
    [DllImport("osbnative")] public static extern void halt();   // sti; hlt
    [DllImport("osbnative")] public static extern void hlt_cpu(); // bare hlt
    [DllImport("osbnative")] public static extern void osb_switch(ulong* saveSp, ulong* loadSp);
    [DllImport("osbnative")] public static extern ulong osb_sched_get_demo_entry();
    [DllImport("osbnative")] public static extern ulong osb_sched_get_user_entry();
    [DllImport("osbnative")] public static extern void osb_poweroff(); // ACPI S5 (QEMU exits)
    [DllImport("osbnative")] public static extern void osb_reboot();   // 8042 reset pulse
    // ring-3 plumbing (boot.S)
    [DllImport("osbnative")] public static extern void osb_lgdt(byte* gdtr);
    [DllImport("osbnative")] public static extern void osb_ltr(ushort selector);
    [DllImport("osbnative")] public static extern void osb_reload_segments();
    [DllImport("osbnative")] public static extern void osb_enter_user(ulong entry, ulong stackTop);
    // per-process address spaces (boot.S)
    [DllImport("osbnative")] public static extern void osb_load_cr3(ulong cr3);
    [DllImport("osbnative")] public static extern ulong osb_master_pml4();
    [DllImport("osbnative")] public static extern ulong osb_master_pdpt();
    [DllImport("osbnative")] public static extern ulong osb_master_pd();
    [DllImport("osbnative")] public static extern int osb_pd_count();
    [DllImport("osbnative")] public static extern ulong osb_kernel_end();
    // user-blob bounds (user.S)
    [DllImport("osbnative")] public static extern ulong osb_userstub_begin();
    [DllImport("osbnative")] public static extern ulong osb_userstub_end();
    [DllImport("osbnative")] public static extern ulong osb_userbad_begin();
    [DllImport("osbnative")] public static extern ulong osb_userbad_end();
    [DllImport("osbnative")] public static extern ulong osb_spawner_begin();
    [DllImport("osbnative")] public static extern ulong osb_spawner_end();
}