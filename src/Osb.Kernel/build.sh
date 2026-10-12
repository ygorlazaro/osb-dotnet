#!/usr/bin/env bash
# Builds the bootable OSB kernel ISO (osb.iso) from src/Osb.Kernel.
#
# Pipeline:
#   1. csc (via the .NET SDK) compiles the freestanding C# against our own
#      mini-CoreLib (MiniCoreLib.cs) -> OsbKernel.dll (managed IL only).
#   2. ilc (NativeAOT) turns that IL into a bare x86-64 object file, with no
#      BCL and no runtime.
#   3. clang assembles the Multiboot2 boot stub (boot.S).
#   4. ld.lld links boot.o + OsbKernel.o with linker.ld into a Multiboot2 ELF.
#   5. grub-mkrescue wraps it in a bootable ISO (GRUB BIOS).
#
# Requires: dotnet (10.x SDK), clang, lld (ld.lld), xorriso, grub-mkrescue.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OBJ="$ROOT/obj/kernel"
ISO_DIR="$ROOT/iso"
OUT_ISO="$ROOT/osb.iso"
OUT_IMG="$ROOT/osb.img"

mkdir -p "$OBJ"

# Generate the ext4 disk image (drive A:) if it does not exist or is older than
# the population script markers (for now, always regenerate small dev image).
echo "[0/6] Building ext4 disk image (A:)"
mkdir -p "$OBJ/fs"
rm -f "$OUT_IMG"
dd if=/dev/zero of="$OUT_IMG" bs=1M count=16 >/dev/null 2>&1
mke2fs -t ext4 -b 4096 \
  -O ^has_journal,^extent,^64bit,^metadata_csum,^flex_bg,^dir_index \
  -L "OSB" "$OUT_IMG" >/dev/null 2>&1
cat > "$OBJ/fs/populate.txt" <<'EOF'
mkdir /OSB
mkdir /OSB/BIN
mkdir /OSB/CONF
mkdir /OSB/HELP
mkdir /OSB/HOME
mkdir /OSB/TEMP
cd /OSB/CONF
write /dev/null /README.txt
quit
EOF
debugfs -w -f "$OBJ/fs/populate.txt" "$OUT_IMG" >/dev/null 2>&1
echo "OK -> $OUT_IMG"

# Locate the native AOT compiler shipped with the SDK (NuGet cache).
ILC="$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/*/tools/ilc 2>/dev/null | sort -V | tail -n1)"
if [[ -z "$ILC" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc (NativeAOT compiler) not found in ~/.nuget/packages." >&2
    echo "Build the project once (dotnet build) or restore the SDK to fetch it." >&2
    exit 1
fi

echo "[1/6] Compiling C# (managed, mini-CoreLib) ..."
dotnet build -c Release "$ROOT/Osb.Kernel.csproj" -v q -nologo

DLL="$ROOT/bin/Release/net10.0/OsbKernel.dll"
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }

echo "[2/6] NativeAOT (ilc) ..."
"$ILC" "$DLL" -o "$OBJ/OsbKernel.o" \
    --systemmodule OsbKernel \
    --targetarch x64 --targetos linux \
    -O -g --directpinvoke:osbnative \
    --map "$OBJ/kernel.map"

echo "[3/6] Generating ISR stubs + assembling ..."
# Generate interrupts.S: one 2-instruction stub per IDT vector plus a common
# frame save/restore entry. Vectors with a CPU error code (8,10-14,17) and a
# uniform [vector][dummy] pushed-frame layout for the C# handler.
ISR="$OBJ/interrupts.S"
: > "$ISR"
printf '.section .text\n\n' >> "$ISR"
ERRVECS=" 8 10 11 12 13 14 17 "
for i in $(seq 0 255); do
  printf '.global isr%s\nisr%s:\n' "$i" "$i" >> "$ISR"
  if [[ "$ERRVECS" == *" $i "* ]]; then
    printf '    addq $8, %%rsp    /* discard CPU error code */\n' >> "$ISR"
  fi
  printf '    pushq $0\n    pushq $%s\n    jmp isr_common\n\n' "$i" >> "$ISR"
done
printf '.global osb_isr_stubs\nosb_isr_stubs:\n' >> "$ISR"
for i in $(seq 0 255); do
  printf '    .quad isr%s\n' "$i" >> "$ISR"
done
cat >> "$ISR" <<'ASM'

.global osb_get_stub_table
.type osb_get_stub_table, @function
osb_get_stub_table:
    leaq osb_isr_stubs(%rip), %rax
    ret

/* Common ISR entry: save all GPR + XMM regs (the C# handler is a SysV
 * function and may clobber caller-saved state, including XMM0-15 that the
 * interrupted C# frame may hold live), realign to 16 bytes, call the
 * dispatcher with a pointer to the saved frame, then restore + iretq. */
isr_common:
    pushq %rax
    pushq %rbx
    pushq %rcx
    pushq %rdx
    pushq %rsi
    pushq %rdi
    pushq %rbp
    pushq %r8
    pushq %r9
    pushq %r10
    pushq %r11
    pushq %r12
    pushq %r13
    pushq %r14
    pushq %r15
    subq $256, %rsp
    movdqu %xmm0,   0(%rsp)
    movdqu %xmm1,  16(%rsp)
    movdqu %xmm2,  32(%rsp)
    movdqu %xmm3,  48(%rsp)
    movdqu %xmm4,  64(%rsp)
    movdqu %xmm5,  80(%rsp)
    movdqu %xmm6,  96(%rsp)
    movdqu %xmm7, 112(%rsp)
    movdqu %xmm8, 128(%rsp)
    movdqu %xmm9, 144(%rsp)
    movdqu %xmm10, 160(%rsp)
    movdqu %xmm11, 176(%rsp)
    movdqu %xmm12, 192(%rsp)
    movdqu %xmm13, 208(%rsp)
    movdqu %xmm14, 224(%rsp)
    movdqu %xmm15, 240(%rsp)
    movq %rsp, %rbp          /* frame base: XMM save area (below the GPRs) */
    andq $-16, %rsp          /* SysV: rsp%16==0 at the call site */
    leaq 256(%rbp), %rdi     /* arg0: pointer to the GPR frame (vector at +120) */
    call osb_handle_interrupt
    movq %rbp, %rsp
    movdqu 240(%rsp), %xmm15
    movdqu 224(%rsp), %xmm14
    movdqu 208(%rsp), %xmm13
    movdqu 192(%rsp), %xmm12
    movdqu 176(%rsp), %xmm11
    movdqu 160(%rsp), %xmm10
    movdqu 144(%rsp), %xmm9
    movdqu 128(%rsp), %xmm8
    movdqu 112(%rsp), %xmm7
    movdqu  96(%rsp), %xmm6
    movdqu  80(%rsp), %xmm5
    movdqu  64(%rsp), %xmm4
    movdqu  48(%rsp), %xmm3
    movdqu  32(%rsp), %xmm2
    movdqu  16(%rsp), %xmm1
    movdqu   0(%rsp), %xmm0
    addq $256, %rsp
    popq %r15
    popq %r14
    popq %r13
    popq %r12
    popq %r11
    popq %r10
    popq %r9
    popq %r8
    popq %rbp
    popq %rdi
    popq %rsi
    popq %rdx
    popq %rcx
    popq %rbx
    popq %rax
    addq $16, %rsp           /* drop [vector][dummy error code] */
    iretq
ASM
clang -c -nostdlib -ffreestanding -o "$OBJ/interrupts.o" "$ISR"
clang -c -nostdlib -ffreestanding -o "$OBJ/boot.o" "$ROOT/boot.S"
clang -c -nostdlib -ffreestanding -o "$OBJ/sched.o" "$ROOT/sched.S"
clang -c -nostdlib -ffreestanding -o "$OBJ/user.o" "$ROOT/user.S"

echo "[4/6] Linking kernel.elf ..."
ld.lld -T "$ROOT/linker.ld" -o "$OBJ/kernel.elf" \
    "$OBJ/boot.o" "$OBJ/interrupts.o" "$OBJ/sched.o" "$OBJ/user.o" "$OBJ/OsbKernel.o" --no-relax

echo "[5/6] Building ISO ..."
rm -rf "$ISO_DIR"
mkdir -p "$ISO_DIR/boot/grub"
cp "$OBJ/kernel.elf" "$ISO_DIR/boot/kernel.elf"
cp "$ROOT/grub.cfg" "$ISO_DIR/boot/grub/grub.cfg"
cp "$OUT_IMG" "$ISO_DIR/osb.img" 2>/dev/null || true
grub-mkrescue -o "$OUT_ISO" "$ISO_DIR" >/dev/null

echo "OK -> $OUT_ISO"

# Helpers
case "${1:-}" in
  run)
    # Headless: serial straight to the terminal, no GUI window to grab the
    # host keyboard/mouse. The kernel mirrors VGA -> COM1, so you still see
    # everything. Quit: `POWEROFF` at the OSB prompt (ACPI S5 -> QEMU exits).
    echo "Booting in QEMU (serial on stdio, no display) ..."
    exec qemu-system-x86_64 -cdrom "$OUT_ISO" -m 64M \
      -drive file="$OUT_IMG",if=ide,index=0 \
      -display none -serial stdio -no-reboot
    ;;
  run-gui)
    # Graphical window (to watch VGA text mode). The window grabs the mouse on
    # click: press Ctrl+Alt+G to release, Ctrl+Alt+2 for the QEMU monitor, or
    # use POWEROFF at the OSB prompt to shut the emulator down cleanly.
    echo "Booting in QEMU (GUI window + serial on stdio) ..."
    echo "  Grab escape: Ctrl+Alt+G  |  monitor: Ctrl+Alt+2 then 'quit'"
    exec qemu-system-x86_64 -cdrom "$OUT_ISO" -m 64M -serial stdio -no-reboot \
      -drive file="$OUT_IMG",if=ide,index=0
    ;;
  check)
    grub-file --is-x86-multiboot2 "$OBJ/kernel.elf" && echo "kernel.elf: Multiboot2 OK"
    ;;
esac