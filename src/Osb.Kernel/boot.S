/* OSB boot stub: Multiboot2 (GRUB) -> 32-bit protected mode -> long mode -> kmain.
 * Also provides the low-level helpers the C# kernel calls (port I/O, mem*). */

.set MB2_MAGIC, 0xE85250D6
.set MB2_ARCH,  0

/* ---------------------------------------------------------------- multiboot2 */
.section .multiboot2, "a"
.align 8
mb2_start:
    .long MB2_MAGIC
    .long MB2_ARCH
    .long mb2_end - mb2_start
    .long -(MB2_MAGIC + MB2_ARCH + (mb2_end - mb2_start))
    .align 8
    .short 0
    .short 0
    .long 8
mb2_end:

/* ---------------------------------------------------------------- data/bss */
.section .data
.align 8
/* Stack canary used by RyuJIT's stackalloc protection (__security_cookie). */
.global __security_cookie
.type __security_cookie, @object
__security_cookie:
    .quad 0x2B992DDFA232

.section .bss
.align 4096
pml4:  .skip 4096
pdpt:  .skip 4096
pd:    .skip 4096
.align 16
stack_bottom: .skip 16384
stack_top:

.section .rodata
.align 8
gdt64:
    .quad 0
    .quad 0x00AF9A000000FFFF     /* null, 64-bit code (DPL0), 64-bit data */
    .quad 0x00AF92000000FFFF
gdt64_ptr:
    .word gdt64_end - gdt64 - 1
    .quad gdt64
gdt64_end:

/* ---------------------------------------------------------------- entry */
.section .text
.code32
.global _start
.type _start, @function
_start:
    cli
    movl $stack_top, %esp
    movl %ebx, %ebp                 /* save multiboot2 info pointer */

    /* zero the three page tables */
    movl $pml4, %edi
    xorl %eax, %eax
    movl $3072, %ecx
    rep stosl

    /* pml4[0] -> pdpt */
    movl $pml4, %edi
    movl $pdpt, %eax
    orl  $3, %eax
    movl %eax, (%edi)

    /* pdpt[0] -> pd */
    movl $pdpt, %edi
    movl $pd, %eax
    orl  $3, %eax
    movl %eax, (%edi)

    /* pd[*]: identity-map the first 1 GiB with 2 MiB pages */
    movl $pd, %edi
    xorl %ecx, %ecx
1:
    movl %ecx, %eax
    shll $21, %eax
    orl  $0x83, %eax
    movl %eax, (%edi,%ecx,8)
    movl $0, 4(%edi,%ecx,8)
    incl %ecx
    cmpl $512, %ecx
    jne 1b

    /* enable PAE */
    movl %cr4, %eax
    orl  $((1<<5) | (1<<9) | (1<<10)), %eax   /* PAE | OSFXSR | OSXMMEXCPT */
    movl %eax, %cr4

    /* load cr3 */
    movl $pml4, %eax
    movl %eax, %cr3

    /* EFER.LME = 1 */
    movl $0xC0000080, %ecx
    rdmsr
    orl  $(1<<8), %eax
    wrmsr

    /* enable paging (enters long mode); force a known-good CR0. Most bootloaders
     * leave EM set, and with CR0.EM=1 every SSE instruction in the C# kernel
     * (e.g. movups to zero a stackalloc buffer) raises #UD. */
    movl %cr0, %eax
    andl $~((1<<2) | (1<<3)), %eax                 /* clear EM, TS */
    orl  $((1<<31) | (1<<5) | (1<<16)), %eax       /* set   PG | NE | WP */
    movl %eax, %cr0

    lgdt gdt64_ptr
    ljmp $0x08, $long_mode

.code64
long_mode:
    movw $0x10, %ax
    movw %ax, %ds
    movw %ax, %es
    movw %ax, %ss
    movw %ax, %fs
    movw %ax, %gs
    leaq stack_top(%rip), %rsp
    movl %ebp, %edi                 /* arg0: multiboot2 info */
    call kmain
2:
    hlt
    jmp 2b

/* ---------------------------------------------------------------- helpers */
.global outb
.type outb, @function
outb:
    movw %di, %dx
    movb %sil, %al
    outb %al, %dx
    ret

.global inb
.type inb, @function
inb:
    movw %di, %dx
    xorl %eax, %eax
    inb  %dx, %al
    ret

.global memcpy
.type memcpy, @function
memcpy:
    movq %rdi, %rax
    movq %rdx, %rcx
    rep movsb
    ret

.global memset
.type memset, @function
memset:
    movq %rdi, %r9
    movq %rdx, %rcx
    movb %sil, %al
    rep stosb
    movq %r9, %rax
    ret

.global memmove
.type memmove, @function
memmove:
    movq %rdi, %rax
    movq %rdx, %rcx
    cmpq %rsi, %rdi
    jbe  3f
    leaq -1(%rdi,%rcx), %rdi
    leaq -1(%rsi,%rcx), %rsi
    std
    rep movsb
    cld
    ret
3:
    rep movsb
    ret

/* ---------------------------------------------------------------- cpu control */
.global lidt
.type lidt, @function
lidt:
    lidt (%rdi)
    ret

.global sti
.type sti, @function
sti:
    sti
    ret

.global cli
.type cli, @function
cli:
    cli
    ret

/* Sleep until the next interrupt (used by the shell idle loop). */
.global halt
.type halt, @function
halt:
    sti
    hlt
    ret

/* hlt honoring IF (used after cli to halt the machine for good). */
.global hlt_cpu
.type hlt_cpu, @function
hlt_cpu:
    hlt
    ret

.section .note.GNU-stack, "", @progbits
