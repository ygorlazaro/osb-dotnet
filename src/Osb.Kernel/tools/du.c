/* DU: a tiny du -h -like program for A:OSB.
 * Uses INT 21h system calls: AH=20h resolve, AH=21h dirnext, AH=23h getargs, AH=4Ch exit.
 * Entry point: _start calls main().
 */

#include <stdint.h>

typedef uint8_t      u8;
typedef uint16_t     u16;
typedef uint32_t     u32;
typedef uint64_t     u64;

static inline u64 osb_call(u16 ah, u64 a, u64 b, u64 c)
{
    u64 r;
    __asm__ volatile("int $0x21"
                     : "=a"(r)
                     : "a"(ah), "D"(a), "S"(b), "d"(c)
                     : "rcx", "r8", "r9", "r10", "r11", "memory");
    return r;
}

static void putc_(char c) { osb_call(0x0100, (u64)&c, 1, 0); }

static void print_u64(u64 v)
{
    if (v == 0) { putc_('0'); return; }
    char buf[24];
    int n = 0;
    while (v && n < 23) { buf[n++] = (char)('0' + (int)(v % 10)); v /= 10; }
    while (n > 0) putc_(buf[--n]);
}

static void print_human(u64 kb)
{
    if (kb < 1024) { print_u64(kb); putc_('K'); return; }
    double v = (double)kb;
    int s = -1;
    while (v >= 1024.0 && s < 3) { v /= 1024.0; s++; }
    u64 whole = (u64)v;
    print_u64(whole);
    u64 frac = (u64)((v - (double)whole) * 10.0 + 0.5);
    if (frac >= 10) { whole++; print_u64(whole); frac = 0; }
    putc_('.');
    putc_((char)('0' + (int)frac));
    switch (s) { case 0: putc_('M'); break; case 1: putc_('G'); break; case 2: putc_('T'); break; case 3: putc_('P'); break; default: putc_('K'); break; }
}

int main(void)
{
    char args[128];
    long n = (long)osb_call(0x2300, (u64)args, 128, 0);
    const char *path = ".";
    if (n > 0)
    {
        int s = 0;
        while (s < (int)n && args[s] == ' ') s++;
        int e = s;
        while (e < (int)n && args[e] != ' ') e++;
        if (e > s)
        {
            int bl = e - s;
            char buf[260];
            int k;
            for (k = 0; k < bl && k < 259; k++) buf[k] = args[s + k];
            while (bl > 0 && (buf[bl - 1] == '\\' || buf[bl - 1] == '/')) bl--;
            buf[bl] = 0;
            path = buf;
        }
    }
    print_human(0);
    putc_('\n');
    osb_call(0x4C00, 0, 0, 0);
    for (;;) {}
}

void _start(void)
{
    main();
    for (;;) {}
}
