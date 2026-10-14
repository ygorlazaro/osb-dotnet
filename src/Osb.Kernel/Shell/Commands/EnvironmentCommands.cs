// SET [NAME]        list variables starting with NAME (or all)
// SET NAME=value    define/redefine an environment variable

using Osb.Kernel.Hardware;
using Osb.Kernel.Shell;

namespace Osb.Kernel.Shell.Commands;

internal static unsafe class EnvironmentCommands
{
    public static void Set(char* line, int len)
    {
        int i = 3;
        while (i < len && line[i] == ' ') i++;
        if (i >= len) { Environment.ListAll(); return; }
        int eq = i;
        while (eq < len && line[eq] != '=') eq++;
        if (eq >= len)
        {
            Environment.ListPrefix(line + i, len - i);
            return;
        }
        int nl = eq - i;
        int vs = eq + 1;
        int vl = len - vs;
        char* nameb = stackalloc char[64];
        char* valb = stackalloc char[256];
        for (int k = 0; k < nl && k < 63; k++) nameb[k] = line[i + k];
        for (int k = 0; k < vl && k < 255; k++) valb[k] = line[vs + k];
        if (!Environment.SetChars(nameb, nl, valb, vl))
            Vga.WriteLine("Cannot set: read-only or out of memory.");
    }
}