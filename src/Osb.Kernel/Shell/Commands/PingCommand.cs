// PING <host> [count]  - ICMP echo over the network stack.
//
// Uses the e1000 driver polled (no interrupts). Resolves a dotted-quad or a
// host on the local subnet, sends echo requests and prints the replies with
// a round-trip time. Like the classic tool, an unanswered probe prints
// nothing until the summary.

using Osb.Kernel.Hardware;
using Osb.Kernel.Network;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class PingCommand
{
    public static void Run(char* line, int len)
    {
        int i = 4;
        while (i < len && line[i] == ' ') i++;
        int hs = i;
        while (i < len && line[i] != ' ') i++;
        int hostLen = i - hs;
        if (hostLen <= 0) { Vga.WriteLine("Usage: PING <host> [count]"); return; }

        uint ip;
        if (!ParseIp(line + hs, hostLen, out ip))
        {
            Vga.Write("PING: cannot resolve host ");
            Vga.WriteStr(line + hs, hostLen);
            Vga.PutChar('\n');
            return;
        }

        int count = 4;
        if (i < len)
        {
            int v = 0;
            bool ok = false;
            while (i < len && line[i] == ' ') i++;
            for (; i < len; i++)
            {
                if (line[i] < '0' || line[i] > '9') { ok = false; break; }
                v = v * 10 + (line[i] - '0'); ok = true;
            }
            if (ok && v > 0) count = v;
        }

        if (!NetStack.Up)
        {
            if (!NetStack.Init())
            {
                Vga.Write("PING: no network interface (error ");
                Vga.WriteDec((ulong)Osb.Kernel.Network.E1000.LastError);
                Vga.WriteLine(")");
                return;
            }
        }
        Vga.Write("PING ");
        WriteIp(ip);
        Vga.Write(" (");

        int got = 0;
        for (int n = 0; n < count; n++)
        {
            // A host that does not answer ARP is reported as an unreachable target,
            // the way the classic tool does, rather than aborting the whole
            // run: the remaining probes should still be attempted.
            if (!NetStack.SendPing(ip, (ushort)(n + 1)))
            {
                Vga.Write("unreachable");
                Vga.PutChar('\n');
                continue;
            }
            // wait for a reply, pumping the stack so echoes are answered too
            bool replied = false;
            ulong start = Osb.Kernel.Cpu.Pit.Ticks;
            ulong deadline = start + 2UL * (ulong)Osb.Kernel.Cpu.Pit.Hz;   // 2 s
            while (Osb.Kernel.Cpu.Pit.Ticks < deadline)
            {
                uint from; ushort seq;
                if (NetStack.Poll(true, out from, out seq) && seq == n + 1)
                {
                    ulong dt = (Osb.Kernel.Cpu.Pit.Ticks - start) * 1000UL / (ulong)Osb.Kernel.Cpu.Pit.Hz;
                    Vga.Write("reply from ");
                    WriteIp(from);
                    Vga.Write(": seq=");
                    Vga.WriteDec((ulong)(n + 1));
                    Vga.Write(" time=");
                    Vga.WriteDec(dt == 0 ? 1 : dt);
                    Vga.WriteLine("ms");
                    replied = true;
                    got++;
                    break;
                }
            }
            if (!replied) Vga.WriteLine("Request timed out.");
        }
        Vga.Write(")");
        Vga.PutChar('\n');
    }

    // Dotted-quad only (no DNS yet): "10.0.2.2"
    private static bool ParseIp(char* p, int len, out uint ip)
    {
        ip = 0;
        uint part = 0;
        int digits = 0;
        int octets = 0;
        for (int i = 0; i <= len; i++)
        {
            if (i == len || p[i] == '.')
            {
                if (digits == 0 || octets >= 4 || part > 255) return false;
                ip = (ip << 8) | (part & 0xFF);
                octets++;
                part = 0;
                digits = 0;
            }
            else if (p[i] >= '0' && p[i] <= '9')
            {
                part = part * 10 + (uint)(p[i] - '0');
                digits++;
                if (digits > 3) return false;
            }
            else return false;
        }
        return octets == 4;
    }

    private static void WriteIp(uint ip)
    {
        Vga.WriteDec((ip >> 24) & 0xFF); Vga.PutChar('.');
        Vga.WriteDec((ip >> 16) & 0xFF); Vga.PutChar('.');
        Vga.WriteDec((ip >> 8) & 0xFF); Vga.PutChar('.');
        Vga.WriteDec(ip & 0xFF);
    }
}
