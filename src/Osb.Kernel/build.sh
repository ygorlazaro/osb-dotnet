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

mkdir -p "$OBJ"

# Locate the native AOT compiler shipped with the SDK (NuGet cache).
ILC="$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/*/tools/ilc 2>/dev/null | sort -V | tail -n1)"
if [[ -z "$ILC" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc (NativeAOT compiler) not found in ~/.nuget/packages." >&2
    echo "Build the project once (dotnet build) or restore the SDK to fetch it." >&2
    exit 1
fi

echo "[1/5] Compiling C# (managed, mini-CoreLib) ..."
dotnet build -c Release "$ROOT/Osb.Kernel.csproj" -v q -nologo

DLL="$ROOT/bin/Release/net10.0/OsbKernel.dll"
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }

echo "[2/5] NativeAOT (ilc) ..."
"$ILC" "$DLL" -o "$OBJ/OsbKernel.o" \
    --systemmodule OsbKernel \
    --targetarch x64 --targetos linux \
    -O -g --directpinvoke:osbnative \
    --map "$OBJ/kernel.map"

echo "[3/5] Assembling boot stub ..."
clang -c -nostdlib -ffreestanding -o "$OBJ/boot.o" "$ROOT/boot.S"

echo "[4/5] Linking kernel.elf ..."
ld.lld -T "$ROOT/linker.ld" -o "$OBJ/kernel.elf" "$OBJ/boot.o" "$OBJ/OsbKernel.o" --no-relax

echo "[5/5] Building ISO ..."
rm -rf "$ISO_DIR"
mkdir -p "$ISO_DIR/boot/grub"
cp "$OBJ/kernel.elf" "$ISO_DIR/boot/kernel.elf"
cp "$ROOT/grub.cfg" "$ISO_DIR/boot/grub/grub.cfg"
grub-mkrescue -o "$OUT_ISO" "$ISO_DIR" >/dev/null

echo "OK -> $OUT_ISO"

# Helpers
case "${1:-}" in
  run)
    echo "Booting in QEMU (serial on stdio) ..."
    exec qemu-system-x86_64 -cdrom "$OUT_ISO" -m 64M -serial stdio -no-reboot
    ;;
  check)
    grub-file --is-x86-multiboot2 "$OBJ/kernel.elf" && echo "kernel.elf: Multiboot2 OK"
    ;;
esac