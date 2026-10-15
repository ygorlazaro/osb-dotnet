#!/usr/bin/env python3
"""Wrap a flat ring-3 image in the OSBEXE header the kernel's loader expects.

Format (little endian), matching Osb.Kernel/Processes/Scheduler.SpawnFile:

    offset 0  : magic "OSBX"
    offset 4  : u16 version      (= 1)
    offset 6  : u16 header size  (= 16)
    offset 8  : u32 entry offset (into the image)
    offset 12 : u32 image size
    offset 16 : image bytes

Usage: mkosbx.py IMAGE.bin OUT.exe [ENTRY_OFFSET]
"""

import struct
import sys


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    src, dst = sys.argv[1], sys.argv[2]
    entry = int(sys.argv[3], 0) if len(sys.argv) > 3 else 0

    with open(src, "rb") as f:
        image = f.read()

    header = b"OSBX" + struct.pack("<HHII", 1, 16, entry, len(image))
    with open(dst, "wb") as f:
        f.write(header)
        f.write(image)

    print(f"wrote {dst}: {len(header) + len(image)} bytes (image {len(image)}, entry {entry:#x})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
