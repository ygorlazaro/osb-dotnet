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
/* pd_pool: one page directory per GiB of the identity map (64 GiB total).
 * pdpt[i] -> pd_pool[i] covers physical [i GiB, (i+1) GiB). */
.align 4096
pd_pool:
    .rept 64
    .skip 4096
    .endr
pd_pool_end:
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

    /* zero the pml4 + pdpt */
    movl $pml4, %edi
    xorl %eax, %eax
    movl $2048, %ecx
    rep stosl

    /* zero the 64 page directories (pd_pool) */
    movl $pd_pool, %edi
    movl $pd_pool_end - pd_pool, %ecx
    shrl $2, %ecx
    rep stosl

    /* pml4[0] -> pdpt (P|RW|US: the user subtree lives under here) */
    movl $pml4, %edi
    movl $pdpt, %eax
    orl  $7, %eax
    movl %eax, (%edi)

    /* pdpt[0..63] -> pd_pool[0..63] (P|RW|US). Each pool entry is one page
     * directory, 4096 bytes apart. */
    movl $pdpt, %edi
    movl $pd_pool, %ebx
    xorl %ecx, %ecx
2:
    movl %ebx, %eax
    orl  $7, %eax
    movl %eax, (%edi,%ecx,8)
    addl $4096, %ebx
    incl %ecx
    cmpl $64, %ecx
    jne 2b

    /* pd_pool[n][j]: identity-map 2 MiB pages across 64 GiB (P|RW|PS).
     * Entry (n,j) covers physical ((n*512 + j) * 2 MiB). */
    movl $pd_pool, %edi
    xorl %ecx, %ecx            /* ecx = flat entry index 0 .. 32767 */
3:
    movl %ecx, %eax
    shll $21, %eax
    orl  $0x83, %eax
    movl %eax, (%edi,%ecx,8)
    movl $0, 4(%edi,%ecx,8)
    incl %ecx
    cmpl $32768, %ecx
    jne 3b

    /* pd[2] = 0x400000..0x5FFFFF: mark U/S=1 so ring-3 code/data is reachable
     * (kernel pages stay U=0 -> ring 3 cannot touch the kernel). */
    movl $0x400087, %eax
    movl %eax, pd_pool+16

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

/* 16-bit port output. */
.global outw
.type outw, @function
outw:
    movw %di, %dx
    movw %si, %ax
    outw %ax, %dx
    ret

.global inb
.type inb, @function
inb:
    movw %di, %dx
    xorl %eax, %eax
    inb  %dx, %al
    ret

/* 16-bit port input (ATA data register is a word port). */
.global inw
.type inw, @function
inw:
    movw %di, %dx
    xorl %eax, %eax
    inw  %dx, %ax
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

/* ACPI S5 soft-off: write 0x2000 (SLP_TYP=0|SLP_EN) to PM1a_CNT (port 0x604).
 * On QEMU this makes the emulator exit cleanly (no window left grabbing input). */
.global osb_poweroff
.type osb_poweroff, @function
osb_poweroff:
    movw $0x604, %dx
    movw $0x2000, %ax
    outw %ax, %dx
    ret

/* Reset via the 8042 keyboard controller (pulse the reset line). */
.global osb_reboot
.type osb_reboot, @function
osb_reboot:
    cli
1:  movw $0x64, %dx
    inb  %dx, %al
    testb $0x02, %al
    jnz  1b
    movw $0xFE, %ax
    outb %al, %dx
2:  hlt
    jmp 2b

/* Load a new GDT from a 10-byte descriptor {limit:16, base:64}. */
.global osb_lgdt
.type osb_lgdt, @function
osb_lgdt:
    lgdt (%rdi)
    ret

/* Load the task register with a TSS selector (argument in %di). */
.global osb_ltr
.type osb_ltr, @function
osb_ltr:
    ltr %di
    ret

/* Reload the data segment registers with the kernel data selector 0x10.
 * CS is left untouched (still 0x08 = kernel code). */
.global osb_reload_segments
.type osb_reload_segments, @function
osb_reload_segments:
    movw $0x10, %ax
    movw %ax, %ds
    movw %ax, %es
    movw %ax, %ss
    movw %ax, %fs
    movw %ax, %gs
    ret

/* Load a new address space (CR3). Argument in %rdi. */
.global osb_load_cr3
.type osb_load_cr3, @function
osb_load_cr3:
    movq %rdi, %cr3
    ret

/* Addresses of the boot (kernel) page tables, so C# can copy the kernel's
 * identity mappings into each process's own page tables.
 *
 * The identity map is 64 GiB, so there is no single "pd" any more: pdpt[i]
 * points at pd_pool[i], and each pd_pool[i] covers one GiB. Processes get
 * their own pdpt and copy the master's PDPT entries (cheap: 64 words), which
 * share the kernel's page directories rather than duplicating them. */
.global osb_master_pml4
.type osb_master_pml4, @function
osb_master_pml4:
    leaq pml4(%rip), %rax
    ret

.global osb_master_pdpt
.type osb_master_pdpt, @function
osb_master_pdpt:
    leaq pdpt(%rip), %rax
    ret

/* Base of the page-directory pool; pd_pool[i] is at base + i*4096 and
 * covers physical [i GiB, (i+1) GiB). */
.global osb_master_pd
.type osb_master_pd, @function
osb_master_pd:
    leaq pd_pool(%rip), %rax
    ret

/* Number of page directories in the pool (= GiB mapped). */
.global osb_pd_count
.type osb_pd_count, @function
osb_pd_count:
    movl $64, %eax
    ret

/* First address past the kernel image (.text + .bss). The C# frame
 * allocator uses it as its floor, so the kernel never hands out frames
 * that overlap its own image or the frame bitmap living in .bss. */
.global osb_kernel_end
.type osb_kernel_end, @function
osb_kernel_end:
    leaq __kernel_end(%rip), %rax
    ret

/* Enter ring 3: rdi = user entry RIP, rsi = user stack top.
 * Builds the iretq frame (SS,RSP,RFLAGS,CS,RIP) and drops to CPL3. Never
 * returns; the kernel stack is (re)entered later via TSS.RSP0 on INT/syscall. */
.global osb_enter_user
.type osb_enter_user, @function
osb_enter_user:
    movq %rdi, %rax          /* RIP  = entry */
    movq %rsi, %rdx          /* RSP  = user stack top */
    movw $0x23, %cx          /* user data selector (0x20 | RPL3) */
    movw %cx, %ds
    movw %cx, %es
    pushq %rcx               /* SS   = 0x23 */
    pushq %rdx               /* RSP */
    pushq $0x202             /* RFLAGS: IF=1 */
    movw $0x1B, %cx          /* user code selector (0x18 | RPL3) */
    pushq %rcx               /* CS   = 0x1B */
    pushq %rax               /* RIP */
    iretq

.section .note.GNU-stack, "", @progbits
