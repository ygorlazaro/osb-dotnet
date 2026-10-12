/* User-mode test programs for the ring-3 milestone.
 *
 * These bytes are position-independent: the kernel copies them to a U/S=1
 * page (0x400000 / 0x401000) and enters them at CPL3. The system API mimics
 * MS-DOS: INT 21h with the function number in AH.
 *
 *   AH=09h  print '$'-terminated string at RSI
 *   AH=4Ch  terminate the program
 */

.section .userstub, "a"
.code64

/* --- normal program: syscall + clean exit ------------------------------- */
.global userstub_begin
.global userstub_end
userstub_begin:
    lea   userstub_msg(%rip), %rsi
    movb  $0x09, %ah                /* AH=09h: print string */
    int   $0x21
    movw  $0x4C00, %ax              /* AH=4Ch, AL=0: terminate (exit code 0) */
    int   $0x21
1:  jmp   1b                        /* never reached */
userstub_msg:
    .asciz "Hello from ring 3! (INT 21h works)\r\n$"
userstub_end:

/* --- bad program: tries a privileged instruction (must fault) ----------- */
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

.section .note.GNU-stack, "", @progbits
