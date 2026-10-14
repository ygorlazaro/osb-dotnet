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
  -O ^has_journal,^metadata_csum,^flex_bg,^dir_index \
  -L "OSB" "$OUT_IMG" >/dev/null 2>&1
cat > "$OBJ/fs/populate.txt" <<'EOF'
mkdir /OSB
mkdir /OSB/BIN
mkdir /OSB/CONF
mkdir /OSB/HELP
mkdir /OSB/HOME
mkdir /OSB/TEMP
mkdir /OSB/APPS
mkdir /OSB/ROOT
mkdir /OSB/ROOT/TRASH
EOF
printf 'OSB File System test.\nCase-sensitive filenames: ON\nHome is A:\\OSB\\ROOT.\n' > "$OBJ/fs/welcome.txt"

# Login system seeds: PASSWD (root, passwordless, uid 0, home A:\OSB\ROOT) and SUDOERS.
printf 'root::0:A:\\OSB\\ROOT\n' > "$OBJ/fs/passwd.txt"
printf 'root\n' > "$OBJ/fs/sudoers.txt"

# Create example OSLANG scripts
mkdir -p "$OBJ/fs/apps"
cp "$ROOT/../Osb.Shell/APPS/KISS/main.osl" "$OBJ/fs/apps/kiss.osl"
cat > "$OBJ/fs/apps/simple.osl" <<'EOF'
USING OSL.CONSOLE

PRINT "Hello from OSLANG 0.72!"
x = 10
y = 20
PRINT "x + y = " + STR(x + y)
IF x < y THEN
    PRINT "x is less than y"
END
PRINT "Testing enum..."
PRINT "KEYCODE = " + STR(KEYCODE)
PRINT "KEYCODE.UNKNOWN = " + STR(KEYCODE.UNKNOWN)
PRINT "KEYCODE.ENTER = " + STR(KEYCODE.ENTER)
PRINT "KEYCODE.ESC = " + STR(KEYCODE.ESC)
PRINT "KEYCODE.SPACE = " + STR(KEYCODE.SPACE)
PRINT "KEYCODE.UP = " + STR(KEYCODE.UP)
PRINT "KEYCODE.DOWN = " + STR(KEYCODE.DOWN)
PRINT "KEYCODE.LEFT = " + STR(KEYCODE.LEFT)
PRINT "KEYCODE.RIGHT = " + STR(KEYCODE.RIGHT)
PRINT "KEYCODE.F1 = " + STR(KEYCODE.F1)
PRINT "KEYCODE.F12 = " + STR(KEYCODE.F12)
PRINT "Testing enum flags (bitwise OR)..."
flags = KEYCODE.UP | KEYCODE.DOWN
PRINT "UP | DOWN = " + STR(flags)
PRINT "Done."
EOF

cat > "$OBJ/fs/apps/hello.osl" <<'EOF'
USING OSL.CONSOLE

FUNCTION MAIN()
    PRINT "Hello from OSLANG 0.72!"
END FUNCTION
EOF

cat > "$OBJ/fs/apps/trycatch.osl" <<'EOF'
USING OSL.CONSOLE

PRINT "=== Testing TRY/CATCH ==="

PRINT "Test 1: Simple TRY without error..."
TRY
    PRINT "In TRY block"
CATCH err
    PRINT "Should not reach here: " + err
END
PRINT "After TRY/CATCH"

PRINT ""
PRINT "Test 2: CATCH division by zero..."
TRY
    x = 1 / 0
    PRINT "Should not reach here"
CATCH err
    PRINT "Caught error: " + err
END
PRINT "After TRY/CATCH - continuing"

PRINT ""
PRINT "Test 3: CATCH invalid array index..."
TRY
    a = [1, 2, 3]
    x = a[10]
    PRINT "Should not reach here"
CATCH err
    PRINT "Caught error: " + err
END
PRINT "After TRY/CATCH - continuing"

PRINT ""
PRINT "All TRY/CATCH tests passed!"
EOF

cat > "$OBJ/fs/apps/test_func.osl" <<'EOF'
USING OSL.CONSOLE
FUNCTION MAIN()
    PRINT "OK"
END FUNCTION
EOF

cat > "$OBJ/fs/apps/enum_demo.osl" <<'EOF'
ENUM PERMISSION
    READ = 1
    WRITE = 2
    EXECUTE = 4
END ENUM

FUNCTION MAIN()
    PRINT "OK"
END FUNCTION
EOF

cat > "$OBJ/fs/apps/class_demo.osl" <<'EOF'
USING OSL.CONSOLE

CLASS Animal
    PUBLIC name
    CONSTRUCTOR(n)
        PRINT "Constructor called with: " + n
        ME.name = n
        PRINT "After assignment, ME.name = " + ME.name
    END CONSTRUCTOR
    FUNCTION Speak()
        PRINT "Animal sound"
    END FUNCTION
END CLASS

CLASS Dog : Animal
    FUNCTION Speak()
        PRINT "Woof! I am " + ME.name
    END FUNCTION
END CLASS

FUNCTION MAIN()
    d = NEW Dog("Rex")
    d.Speak()
    PRINT "Class demo done."
END FUNCTION
EOF

cat > "$OBJ/fs/apps/simple_class.osl" <<'EOF'
USING OSL.CONSOLE

CLASS Point
    PUBLIC x
    PUBLIC y
    CONSTRUCTOR(x, y)
        ME.x = x
        ME.y = y
    END CONSTRUCTOR
    FUNCTION Display()
        PRINT "Point: " + ME.x + ", " + ME.y
    END FUNCTION
END CLASS

FUNCTION MAIN()
    p = NEW Point(10, 20)
    p.Display()
    PRINT "Simple class test done."
END FUNCTION
EOF

cat > "$OBJ/fs/apps/dowhile.osl" <<'EOF'
USING OSL.CONSOLE

PRINT "Testing DO WHILE with comparison..."
i = 0
DO
    PRINT "i = " + STR(i)
    i = i + 1
WHILE i < 3
PRINT "Comparison test done!"

PRINT ""
PRINT "Testing DO WHILE with FALSE..."
j = 0
DO
    PRINT "j = " + STR(j)
    j = j + 1
WHILE FALSE
PRINT "FALSE test done (loop should not repeat)!"
EOF

# Regression for the runtime budgets (B5-B7): recursion to a legal depth, a
# runaway recursion that must produce a catchable error instead of smashing the
# 16 KiB boot stack, and KEY enum lookups that walk the whole member table.
# Run it with: OSLANG A:\OSB\APPS\PHASE0.OSL
cat > "$OBJ/fs/apps/phase0.osl" <<'EOF'
FUNCTION FACT(n)
    IF n <= 1 THEN
        RETURN 1
    END
    RETURN n * FACT(n - 1)
END FUNCTION

FUNCTION DOWN(n)
    RETURN DOWN(n + 1)
END FUNCTION

FUNCTION MAIN()
    PRINT "-- recursion within the limit --"
    PRINT "5! = " + FACT(5)
    PRINT "8! = " + FACT(8)

    PRINT "-- runaway recursion is caught --"
    TRY
        PRINT DOWN(0)
        PRINT "never"
    CATCH e
        PRINT "caught: " + e
    END
    PRINT "still alive"

    PRINT "-- the KEYCODE enum table is complete --"
    PRINT KEYCODE.UNKNOWN
    PRINT KEYCODE.ENTER
    PRINT KEYCODE.ESC
    PRINT KEYCODE.BACKSPACE
    PRINT KEYCODE.PAGEDOWN
    PRINT KEYCODE.F1
    PRINT KEYCODE.F12

    PRINT "DONE"
END FUNCTION
EOF

# Regression for the Phase 1 interpreter fixes: field writes through a bare
# identifier (B2), array literals and indexing (B3), and CATCH across a call
# boundary (B4). Run it with: OSLANG A:\OSB\APPS\PHASE1.OSL
cat > "$OBJ/fs/apps/phase1.osl" <<'EOF'
USING OSL.CONSOLE

FUNCTION RAISER()
    RETURN NOSUCHNAME
END FUNCTION

CLASS BOX
    PUBLIC VALUE
    CONSTRUCTOR(v)
        VALUE = v
    END CONSTRUCTOR
    FUNCTION PUT(v)
        VALUE = v
    END FUNCTION
    FUNCTION GET()
        RETURN VALUE
    END FUNCTION
END CLASS

FUNCTION SHADOW()
    VALUE = "local"
    PRINT "fn sees: " + VALUE
END FUNCTION

FUNCTION MAIN()
    PRINT "-- 1. constructor writes the field (bare name) --"
    b = NEW BOX(5)
    PRINT "value=" + b.GET()

    PRINT "-- 2. method writes the field, then reads it --"
    b.PUT(9)
    PRINT "value=" + b.GET()

    PRINT "-- 3. a local with the same name still wins --"
    SHADOW()
    PRINT "field untouched: " + b.GET()

    PRINT "-- 4. arrays --"
    a = [10, 20, 30]
    PRINT "count=" + COUNT(a)
    PRINT "a[0]=" + a[0] + " a[2]=" + a[2]
    a[1] = 99
    PRINT "a[1]=" + a[1]
    m = [[1, 2], [3, 4]]
    PRINT "nested m[1][0]=" + m[1][0]

    PRINT "-- 5. CATCH across a call --"
    TRY
        RAISER()
        PRINT "never"
    CATCH e1
        PRINT "caught call: " + e1
    END

    PRINT "-- 6. CATCH inside a method --"
    TRY
        b.PUT(NOSUCHNAME)
    CATCH e2
        PRINT "caught method: " + e2
    END

    PRINT "-- 7. globals --"
    GLOBAL G = 42
    PRINT "G=" + G

    PRINT "DONE"
END FUNCTION
EOF

# Regression for the Phase 2 features: methods on primitive values, the MATH
# namespace, SWITCH, postfix ++/--, inherited and self method dispatch, member
# visibility, and printing arrays/objects. Run it with:
#   OSLANG A:\OSB\APPS\PHASE2.OSL
cat > "$OBJ/fs/apps/phase2.osl" <<'EOF'
CLASS SHAPE
    PRIVATE ID
    PROTECTED SIDES
    PUBLIC NAME
    CONSTRUCTOR(label, sideCount)
        ID = "s" + sideCount
        SIDES = sideCount
        NAME = label
    END CONSTRUCTOR
    PUBLIC FUNCTION AREA()
        RETURN SIDES
    END FUNCTION
    PUBLIC FUNCTION TAG()
        RETURN ID
    END FUNCTION
    PRIVATE FUNCTION SECRET()
        RETURN 7
    END FUNCTION
    PUBLIC FUNCTION VIA()
        RETURN SECRET()
    END FUNCTION
    PUBLIC FUNCTION SHOW()
        PRINT "shape " + NAME + " sides=" + SIDES
    END FUNCTION
END CLASS

CLASS SQUARE : SHAPE
    CONSTRUCTOR(label)
        BASE(label, 4)
    END CONSTRUCTOR
    FUNCTION TWICE()
        ME.SHOW()
        ME.SHOW()
    END FUNCTION
    FUNCTION SIDECOUNT()
        RETURN SIDES
    END FUNCTION
END CLASS

FUNCTION GRADE(score)
    SWITCH score
        CASE 10
            PRINT "perfect"
        CASE 7
            PRINT "good"
        CASE 4
            PRINT "poor"
        DEFAULT
            PRINT "fail"
    END
END FUNCTION

FUNCTION MAIN()
    PRINT "-- 1. STRING methods --"
    PRINT "  " + "abc".TOUPPER() + " " + "ABC".TOLOWER()
    PRINT "  [" + "  pad  ".TRIM() + "] len=" + "hello".LENGTH
    PRINT "  " + "hello".STARTSWITH("he") + " " + "hello".ENDSWITH("LO")
    PRINT "  idx=" + "hello".INDEXOF("l") + " sub=" + "hello".SUBSTR(1, 3)
    PRINT "  " + "a-b-c".REPLACE("-", "+") + " " + "ab".REPEAT(2)
    PRINT "  [" + "7".PADSTART(3, "0") + "] " + "a,b".SPLIT(",").JOIN("/")

    PRINT "-- 2. NUMBER methods --"
    PRINT "  " + (3.7).ABS() + " " + 3.2.CEIL() + " " + 3.9.FLOOR()
    PRINT "  " + 3.0.ISINTEGER() + " " + 5.BETWEEN(1, 10) + " " + (3.14159).TRUNC(2)

    PRINT "-- 3. ARRAY methods --"
    a = [3, 1, 2]
    PRINT "  " + a.COUNT + " first=" + a.FIRST + " last=" + a.LAST
    a.ADD(4)
    a.SORT()
    PRINT "  " + a + " joined=" + a.JOIN(",")
    PRINT "  popped=" + a.POP() + " -> " + a
    PRINT "  has 2: " + a.CONTAINS(2) + " nested " + [[1, 2], [3]]

    PRINT "-- 4. MATH namespace --"
    PRINT "  " + MATH.SQRT(16) + " " + MATH.POW(2, 8) + " " + MATH.MIN(3, 8)
    PRINT "  " + MATH.SIGN(-9) + " " + MATH.ROUND(2.567, 1) + " " + MATH.CLAMP(15, 0, 10)

    PRINT "-- 5. SWITCH --"
    GRADE(10)
    GRADE(7)
    GRADE(4)
    GRADE(1)

    PRINT "-- 6. postfix ++ / -- --"
    i = 5
    PRINT "  i++ -> " + i++ + " now " + i
    j = 5
    PRINT "  j-- -> " + j-- + " now " + j
    k = 0
    PRINT "  a[k++] -> " + a[k++]

    PRINT "-- 7. inheritance and self dispatch --"
    sq = NEW SQUARE("box")
    sq.TWICE()
    PRINT "  area=" + sq.AREA() + " tag=" + sq.TAG()
    PRINT "  protected via subclass=" + sq.SIDECOUNT()
    PRINT "  private via own method=" + sq.VIA()
    TRY
        sq.SECRET()
    CATCH e3
        PRINT "  caught: " + e3
    END

    PRINT "-- 8. TYPEOF and printing --"
    PRINT "  " + TYPEOF(1) + " " + TYPEOF("s") + " " + TYPEOF(a) + " " + TYPEOF(sq)

    PRINT "DONE"
END FUNCTION
EOF

{ cat "$OBJ/fs/populate.txt"; printf 'write "%s" /OSB/README.TXT\n' "$OBJ/fs/welcome.txt"; printf 'write "%s" /OSB/APPS/PHASE0.OSL\n' "$OBJ/fs/apps/phase0.osl"; printf 'write "%s" /OSB/APPS/PHASE1.OSL\n' "$OBJ/fs/apps/phase1.osl"; printf 'write "%s" /OSB/APPS/PHASE2.OSL\n' "$OBJ/fs/apps/phase2.osl"; printf 'write "%s" /OSB/APPS/SIMPLE.OSL\n' "$OBJ/fs/apps/simple.osl"; printf 'write "%s" /OSB/APPS/HELLO.OSL\n' "$OBJ/fs/apps/hello.osl"; printf 'write "%s" /OSB/BIN/KISS.OSL\n' "$OBJ/fs/apps/kiss.osl"; printf 'write "%s" /OSB/APPS/CLASS_DEMO.OSL\n' "$OBJ/fs/apps/class_demo.osl"; printf 'write "%s" /OSB/APPS/SIMPLE_CLASS.OSL\n' "$OBJ/fs/apps/simple_class.osl"; printf 'write "%s" /OSB/APPS/ENUM_DEMO.OSL\n' "$OBJ/fs/apps/enum_demo.osl"; printf 'write "%s" /OSB/APPS/DOWHILE.OSL\n' "$OBJ/fs/apps/dowhile.osl"; printf 'write "%s" /OSB/APPS/TRYCATCH.OSL\n' "$OBJ/fs/apps/trycatch.osl"; printf 'write "%s" /OSB/APPS/TEST_FUNC.OSL\n' "$OBJ/fs/apps/test_func.osl"; printf 'write "%s" /OSB/CONF/PASSWD\n' "$OBJ/fs/passwd.txt"; printf 'write "%s" /OSB/CONF/SUDOERS\n' "$OBJ/fs/sudoers.txt"; printf 'sif /OSB/TEMP mode 040777\n'; printf 'sif /OSB/CONF/PASSWD mode 0100600\n'; printf 'sif /OSB/CONF/SUDOERS mode 0100600\n'; } > "$OBJ/fs/populate2.txt"
debugfs -w -f "$OBJ/fs/populate2.txt" "$OUT_IMG" >/dev/null 2>&1
# debugfs "write" does not update the inode/block bitmaps: reconcile them or the
# kernel's allocators would hand out the seeded files' inodes (e.g. SUDOERS).
e2fsck -fy "$OUT_IMG" >/dev/null 2>&1
echo "OK -> $OUT_IMG"

# ----- compile the external BIN programs (MEM.EXE & DU.EXE) -----
echo "[6/6] Compiling external BIN programs ..."
# MEM/DU are C# programs compiled to AOT native binaries and wrapped as OSBEXE.
# They are prepared separately (mem_exe.bin/du_exe.bin) and written into /OSB/BIN.

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
    # OSB_MEM overrides the guest RAM size (default 64M); the kernel maps up
    # to 64 GiB, so values like 2G exercise the multi-directory identity map.
    exec qemu-system-x86_64 -cdrom "$OUT_ISO" -m "${OSB_MEM:-64M}" \
      -drive file="$OUT_IMG",if=ide,index=0 \
      -display none -serial stdio -no-reboot
    ;;
  run-gui)
    # Graphical window (to watch VGA text mode). The window grabs the mouse on
    # click: press Ctrl+Alt+G to release, Ctrl+Alt+2 for the QEMU monitor, or
    # use POWEROFF at the OSB prompt to shut the emulator down cleanly.
    echo "Booting in QEMU (GUI window + serial on stdio) ..."
    echo "  Grab escape: Ctrl+Alt+G  |  monitor: Ctrl+Alt+2 then 'quit'"
    exec qemu-system-x86_64 -cdrom "$OUT_ISO" -m "${OSB_MEM:-64M}" -serial stdio -no-reboot \
      -drive file="$OUT_IMG",if=ide,index=0
    ;;
  check)
    grub-file --is-x86-multiboot2 "$OBJ/kernel.elf" && echo "kernel.elf: Multiboot2 OK"
    ;;
esac