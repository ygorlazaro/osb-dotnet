/* Minimal cooperative context switch for the round-robin scheduler.
 *
 * osb_switch(ulong* save_sp, ulong* load_sp): saves the callee-saved
 * registers of the current task on its own stack and switches to the saved
 * stack of the next task, resuming right after its own osb_switch call.
 * Volatile registers (including XMM) are not saved: SysV caller-saved state
 * is spilled/reloaded by the compiler around the call, so this is ABI-safe.
 */

.section .text

.global osb_switch
.type osb_switch, @function
osb_switch:
    pushq %rbx
    pushq %rbp
    pushq %r12
    pushq %r13
    pushq %r14
    pushq %r15
    movq %rsp, (%rdi)          /* save current task's SP */
    movq (%rsi), %rsp          /* load next task's SP */
    popq %r15
    popq %r14
    popq %r13
    popq %r12
    popq %rbp
    popq %rbx
    ret

/* First time the demo task is scheduled in, its seeded stack "returns" here.
 * Enables interrupts (we arrive through the timer's interrupt gate, IF=0) and
 * calls the C# task body (which never returns). */
.global osb_demo_trampoline
.type osb_demo_trampoline, @function
osb_demo_trampoline:
    sti
    call osb_demo_task_main
1:  hlt
    jmp 1b

/* Expose the trampoline address to C# so it can seed the initial stack. */
.global osb_sched_get_demo_entry
.type osb_sched_get_demo_entry, @function
osb_sched_get_demo_entry:
    leaq osb_demo_trampoline(%rip), %rax
    ret

/* First time the user task is scheduled in, its seeded stack "returns" here.
 * Runs on the task's kernel stack and calls the C# body, which sets up and
 * drops to ring 3 (osb_enter_user) and never returns. */
.global osb_user_entry
.type osb_user_entry, @function
osb_user_entry:
    call osb_user_task_main
1:  hlt
    jmp 1b

/* Expose the user-task entry address to C# for stack seeding. */
.global osb_sched_get_user_entry
.type osb_sched_get_user_entry, @function
osb_sched_get_user_entry:
    leaq osb_user_entry(%rip), %rax
    ret

.section .note.GNU-stack, "", @progbits