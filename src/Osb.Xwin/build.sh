#!/usr/bin/env bash
# Build XWIN.EXE - the OSB graphical shell as a ring-3 NativeAOT program.
#
# Steps: managed build (mini-CoreLib) -> ilc native object -> link at
# 0x2000000 (user.ld) -> flat binary -> OSBEXE header (mkosbx.py).
#
# The result is written to obj/xwin/XWIN.EXE and (when STAGE_BIN is set) copied
# to $STAGE_BIN, where the kernel image build picks it up for /OSB/BIN.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OBJ="$ROOT/obj/xwin"
mkdir -p "$OBJ"

# Locate the native AOT compiler shipped with the SDK (NuGet cache).
ILC="${ILC:-$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/*/tools/ilc 2>/dev/null | sort -V | tail -n1 || true)}"
if [[ -z "${ILC:-}" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc (NativeAOT compiler) not found in ~/.nuget/packages." >&2
    exit 1
fi

for tool in clang ld.lld objcopy nm python3; do
    command -v "$tool" >/dev/null || { echo "ERROR: $tool not found." >&2; exit 1; }
done

echo "[1/5] Compiling C# (managed, mini-CoreLib) ..."
dotnet build -c Release "$ROOT/Osb.Xwin.csproj" -v q -nologo

DLL="$ROOT/bin/Release/net10.0/OsbXwin.dll"
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }

echo "[2/5] NativeAOT (ilc) ..."
"$ILC" "$DLL" -o "$OBJ/OsbXwin.o" \
    --systemmodule OsbXwin \
    --targetarch x64 --targetos linux \
    -O -g --directpinvoke:osbsys

echo "[3/5] Assembling ring-3 entry/syscall shim ..."
clang -c -nostdlib -ffreestanding -o "$OBJ/xwin.o" "$ROOT/xwin.S"

echo "[4/5] Linking user image at 0x2000000 ..."
ld.lld -T "$ROOT/user.ld" -o "$OBJ/xwin.elf" \
    "$OBJ/xwin.o" "$OBJ/OsbXwin.o" --no-relax

objcopy -O binary "$OBJ/xwin.elf" "$OBJ/xwin.bin"

# Entry offset = _start - UserBase, so the image can be relocated freely.
ENTRY_OFF="$(nm "$OBJ/xwin.elf" | awk '$3=="_start"{print $1}' | head -n1)"
ENTRY_OFF="$(python3 -c "import sys; print(int(sys.argv[1],16)-0x2000000)" "0x${ENTRY_OFF:-400000}")"

echo "[5/5] Wrapping OSBEXE ..."
python3 "$ROOT/mkosbx.py" "$OBJ/xwin.bin" "$OBJ/XWIN.EXE" "$ENTRY_OFF"

if [[ -n "${STAGE_BIN:-}" ]]; then
    mkdir -p "$STAGE_BIN"
    cp "$OBJ/XWIN.EXE" "$STAGE_BIN/XWIN.EXE"
    echo "staged $STAGE_BIN/XWIN.EXE"
fi

echo "OK -> $OBJ/XWIN.EXE"
