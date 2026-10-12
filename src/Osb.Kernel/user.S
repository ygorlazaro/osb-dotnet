/* User-mode programs for the process milestone.
 *
 * These bytes are position-independent: each process gets a private 2 MiB
 * block mapped at 0x400000, and the kernel copies one program there and enters
 * it at CPL3. The system API mimics MS-DOS: INT 21h with AH = function.
 *
 *   AH=09h  print '$'-terminated string at RSI
 *   AH=30h  getpid -> EAX
 *   AH=4Ch  exit(code in AL)
 *   AH=80h  spawn(program id in AL) -> child pid in EAX
 *   AH=81h  wait(pid in RSI) -> child exit code in EAX
 */

.section .userstub, "a"
.code64

/* --- HELLO: proves per-process memory isolation ------------------------- *
 * Writes its own pid into two slots (secret @0x500000, expected @0x510000),
 * prints a message, spins long enough for the scheduler to run another HELLO,
 * then checks that the secret still matches. If memory were shared, the other
 * process's store would clobber it. */
.global userstub_begin
.global userstub_end
userstub_begin:
    movl  $0x3000, %eax             /* AH=30h: getpid -> EAX */
    int   $0x21
    movl  $0x500000, %ebx
    movl  %eax, (%ebx)              /* secret   = pid */
    movl  $0x510000, %ebx
    movl  %eax, (%ebx)              /* expected = pid */

    lea   userstub_msg(%rip), %rsi
    movb  $0x09, %ah
    int   $0x21

    movl  $0x2, %edx                /* outer: ~0.8 s, long enough for two   */
5:  movl  $0x4000000, %ecx          /* HELLO processes to overlap, so a     */
1:  decl  %ecx                      /* shared page would be detected        */
    jnz   1b
    decl  %edx
    jnz   5b

    movl  $0x500000, %ebx
    movl  (%ebx), %eax             /* read back the secret */
    movl  $0x510000, %ebx
    cmpl  (%ebx), %eax
    jne   2f
    lea   userstub_ok(%rip), %rsi
    jmp   3f
2:  lea   userstub_bad(%rip), %rsi
3:  movb  $0x09, %ah
    int   $0x21

    movw  $0x4C00, %ax              /* AH=4Ch, AL=0: exit(0) */
    int   $0x21
4:  jmp   4b

userstub_msg:
    .asciz "HELLO from ring 3;\r\n$"
userstub_ok:
    .asciz "memory is private (OK)\r\n$"
userstub_bad:
    .asciz "MEMORY CORRUPTED (shared?)\r\n$"
userstub_end:

/* --- BAD: tries a privileged instruction (must fault) ------------------- */
.global userbad_begin
.global userbad_end
userbad_begin:
    cli                             /* privileged -> #GP at CPL3 */
    lea   userbad_msg(%rip), %rsi   /* if we get here, protection failed */
    movb  $0x09, %ah
    int   $0x21
    movb  $0x4c, %ah
    int   $0x21
1:  jmp   1b
userbad_msg:
    .asciz "!!! ring-3 protection FAILED: cli ran at CPL3\r\n$"
userbad_end:

/* --- SPAWNER: exercises the process syscalls from ring 3 -----------------
 * spawn(BAD) -> wait(child) -> reaps it. BAD faults (cli at CPL3) so its exit
 * code is 255. Proves AH=80h/81h work while another process owns a private
 * address space. */
.global spawner_begin
.global spawner_end
spawner_begin:
    movl  $0x8001, %eax             /* AH=80h spawn, AL=1 (BAD) */
    int   $0x21
    movl  %eax, %esi                /* child pid -> RSI for wait */
    movl  $0x8100, %eax             /* AH=81h wait(pid) */
    int   $0x21
    cmpl  $0xFF, %eax               /* BAD faults -> exit code 255 */
    jne   1f
    lea   spawner_ok(%rip), %rsi
    jmp   2f
1:  lea   spawner_bad(%rip), %rsi
2:  movb  $0x09, %ah
    int   $0x21
    movw  $0x4C00, %ax              /* exit(0) */
    int   $0x21
3:  jmp   3b
spawner_ok:
    .asciz "SPAWNER: spawn+wait+reap from ring 3 OK (child code 255)\r\n$"
spawner_bad:
    .asciz "SPAWNER: unexpected wait result\r\n$"
spawner_end:

/* --- helpers so C# can find the blob bounds ----------------------------- */
.global osb_userstub_begin
.type osb_userstub_begin, @function
osb_userstub_begin:
    leaq userstub_begin(%rip), %rax
    ret

.global osb_userstub_end
.type osb_userstub_end, @function
osb_userstub_end:
    leaq userstub_end(%rip), %rax
    ret

.global osb_userbad_begin
.type osb_userbad_begin, @function
osb_userbad_begin:
    leaq userbad_begin(%rip), %rax
    ret

.global osb_userbad_end
.type osb_userbad_end, @function
osb_userbad_end:
    leaq userbad_end(%rip), %rax
    ret

.global osb_spawner_begin
.type osb_spawner_begin, @function
osb_spawner_begin:
    leaq spawner_begin(%rip), %rax
    ret

.global osb_spawner_end
.type osb_spawner_end, @function
osb_spawner_end:
    leaq spawner_end(%rip), %rax
    ret

.section .note.GNU-stack, "", @progbits
