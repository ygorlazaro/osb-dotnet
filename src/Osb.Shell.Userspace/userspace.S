/* Ring-3 entry, syscall gate, and runtime helpers for the OSB userspace shell. */

.section .text

.global _start
.type _start, @function
_start:
    andq $-16, %rsp
    call OsbShell__Module___StartupCodeMain
1:  hlt
    jmp 1b

/* HandleSyscall(frame): load the DOS-style register image, INT 21h, store result. */
.global HandleSyscall
.type HandleSyscall, @function
HandleSyscall:
    movq %rdi, %r8
    movq 112(%r8), %rax
    movq 96(%r8), %rcx
    movq 88(%r8), %rdx
    movq 80(%r8), %rsi
    movq 72(%r8), %rdi
    int $0x21
    movq %rax, 112(%r8)
    ret

/* runtime helpers the AOT code may call */
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

.section .data
.align 8
.global __security_cookie
.type __security_cookie, @object
__security_cookie:
    .quad 0x2B992DDFA232

.section .note.GNU-stack, "", @progbits
