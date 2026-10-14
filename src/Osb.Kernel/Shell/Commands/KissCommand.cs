using Osb.Kernel.Hardware;
using Osb.Kernel.Memory;

namespace Osb.Kernel.Shell;

internal static unsafe class KissCommand
{
    public static void Run(char* line, int len)
    {
        // Find the argument part after KISS.
        int i = 4; // "KISS"
        while (i < len && line[i] == ' ') i++;
        while (i < len && line[i] != ' ') i++;
        int argS = i;
        while (argS < len && line[argS] == ' ') argS++;

        char* kline = stackalloc char[256];
        int kl = 0;
        fixed (char* c = "OSLANG")
        {
            for (int k = 0; c[k] != '\0'; k++) kline[kl++] = c[k];
        }
        kline[kl++] = ' ';
        fixed (char* p = "A:\\OSB\\BIN\\KISS.OSL")
        {
            for (int k = 0; p[k] != '\0'; k++) kline[kl++] = p[k];
        }
        if (argS < len)
        {
            kline[kl++] = ' ';
            for (int k = argS; k < len && kl < 250; k++) kline[kl++] = line[k];
        }
        kline[kl] = '\0';
        global::Kernel.OslangCmd(kline, kl);
    }
}