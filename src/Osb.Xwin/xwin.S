/* XWIN ring-3 entry, syscall gate, and the runtime helpers the NativeAOT
 * compiler expects to find as native symbols.
 *
 * The kernel loads this image at VA 0x2000000 (see Osb.Kernel Memory/Layout.cs)
 * and enters it at _start in ring 3. Everything here is position-dependent
 * (linked at 0x2000000) and self-contained. */

.section .text

/* Entry: align the stack (SysV wants 16-byte alignment at the call site) and
 * hand off to the managed entry exported by Program.cs. */
.global _start
.type _start, @function
_start:
    andq $-16, %rsp
    call xwin_main
1:  hlt
    jmp 1b

/* osb_sys(fn, a, b, c, d): rotate the SysV arguments into the INT 21h ABI.
 *   in : RDI=fn RSI=a RDX=b RCX=c R8=d
 *   out: RAX=fn RDI=a RSI=b RDX=c RCX=d, then `int $0x21`, result in RAX. */
.global osb_sys
.type osb_sys, @function
osb_sys:
    movq %rdi, %rax
    movq %rsi, %rdi
    movq %rdx, %rsi
    movq %rcx, %rdx
    movq %r8,  %rcx
    int  $0x21
    ret

/* ---- runtime helpers the AOT code may call ---- */

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
3:  rep movsb
    ret

/* RyuJIT's stackalloc protection reads this cookie. */
.section .data
.align 8
.global __security_cookie
.type __security_cookie, @object
__security_cookie:
    .quad 0x2B992DDFA232

.section .note.GNU-stack, "", @progbits
