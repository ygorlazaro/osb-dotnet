// DOS-style prompt, rendered from $PROMPT.
//
// $P path  $N drive  $G >  $L <  $B |  $Q =  $S space  $D date  $T time
// $V version  $H backspace  $E ESC  $_ newline  $$ literal $, or any
// variable such as $USER/$HOME.

using Osb.Kernel.Cpu;
using Osb.Kernel.Hardware;
using Osb.Kernel.Storage;

namespace Osb.Kernel.Shell;

internal static unsafe class Prompt
{
    public static void Write()
    {
        char* prompt = stackalloc char[128];
        int plen;
        fixed (char* pr = "PROMPT")
        {
            int po, pl;
            if (Environment.TryGet(pr, 6, out po, out pl))
            {
                plen = pl < 127 ? pl : 127;
                for (int i = 0; i < plen; i++) prompt[i] = Environment.ValueAt(po + i);
            }
            else
            {
                prompt[0] = '$'; prompt[1] = 'P'; prompt[2] = '$'; prompt[3] = 'G';
                plen = 4;
            }
        }
        prompt[plen] = '\0';
        for (int k = 0; k < plen; k++)
        {
            if (prompt[k] != '$') { Vga.PutChar(prompt[k]); continue; }
            if (k + 1 >= plen) { Vga.PutChar('$'); continue; }
            char t = ShellText.Up(prompt[k + 1]);
            bool known = t == 'N' || t == 'P' || t == 'G' || t == 'L' || t == 'B' ||
                         t == 'Q' || t == 'S' || t == 'D' || t == 'T' || t == 'V' ||
                         t == 'H' || t == 'E' || t == '$' || t == '_';
            if (known)
            {
                k++;
                switch (t)
                {
                    case 'N': Vga.PutChar(Cwd.Drive); break;
                    case 'G': Vga.PutChar('>'); break;
                    case 'L': Vga.PutChar('<'); break;
                    case 'B': Vga.PutChar('|'); break;
                    case 'Q': Vga.PutChar('='); break;
                    case 'S': Vga.PutChar(' '); break;
                    case 'D': Rtc.PrintDate(); break;
                    case 'T': Rtc.PrintTime(); break;
                    case 'V': Vga.Write("OSB 0.1"); break;
                    case '$': Vga.PutChar('$'); break;
                    case 'H': Vga.PutChar('\b'); break;
                    case 'E': Vga.PutChar((char)27); break;
                    case '_': Vga.PutChar('\n'); break;
                    case 'P': Cwd.Print(); break;
                }
                continue;
            }
            int st = k + 1;
            int j = st;
            while (j < plen && (((prompt[j] >= 'A' && prompt[j] <= 'Z') || (prompt[j] >= 'a' && prompt[j] <= 'z') ||
                                (prompt[j] >= '0' && prompt[j] <= '9') || prompt[j] == '_'))) j++;
            if (j > st)
            {
                int nameLen = j - st;
                char* nm = stackalloc char[64];
                for (int q = 0; q < nameLen && q < 63; q++) nm[q] = prompt[st + q];
                int vo, vl;
                if (Environment.TryGet(nm, nameLen, out vo, out vl))
                {
                    for (int q = 0; q < vl; q++) Vga.PutChar(Environment.ValueAt(vo + q));
                    k = j - 1;
                    continue;
                }
                Vga.PutChar('$');
                for (int q = st; q < j; q++) Vga.PutChar(prompt[q]);
                k = j - 1;
                continue;
            }
            Vga.PutChar('$');
        }
    }
}