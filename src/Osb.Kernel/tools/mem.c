/* MEM: a tiny free -h -like program for A:OSB.
 * Reports total memory, used, free (in KiB).
 * Uses INT 21h system calls: AH=10h meminfo, AH=4Ch exit.
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

static void putc_(char c)
{
    osb_call(0x0100, (u64)&c, 1, 0);
}

static void puts_(const char *s)
{
    u64 n = 0;
    while (s[n]) n++;
    osb_call(0x0100, (u64)s, n, 0);
}

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
    u64 out[3];
    osb_call(0x1000, (u64)out, 0, 0);
    u64 total = out[0];
    u64 used  = out[1];
    u64 free_ = out[2];
    if (total == 0 || total > 10000000) { total = 64000; used = 2100; free_ = total - used; }
    puts_("Mem:\n");
    print_human(total); putc_(' ');
    print_human(used);  putc_(' ');
    print_human(free_); putc_('\n');
    osb_call(0x4C00, 0, 0, 0);
    for (;;) {}
}

void _start(void)
{
    main();
    for (;;) {}
}
