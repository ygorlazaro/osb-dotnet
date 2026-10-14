#!/usr/bin/env python3
"""Baseline probe: does ANY guest get a graphics mode out of this QEMU?

Boots an ISO whose default GRUB entry switches to gfxterm and sits there.
If the "GRAPHICS BASELINE" line comes out in a non-text resolution, the
display path can render graphics; if GRUB reports "no suitable video mode
found" and stays at 720x400, it cannot - and no amount of VGA programming
in our kernel will show anything.

Usage: baseline.py <iso> [display-args...]
"""
import os
import socket
import subprocess
import sys
import time

MON = 4620


def probe(iso, extra, label, wait=20):
    proc = subprocess.Popen(
        ["qemu-system-x86_64", "-cdrom", iso, "-m", "128M",
         "-serial", "stdio", "-no-reboot",
         "-monitor", "tcp:127.0.0.1:%d,server,nowait" % MON] + extra,
        stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL)
    ppm = "/tmp/_base_%d.ppm" % os.getpid()
    try:
        time.sleep(wait)
        s = socket.create_connection(("127.0.0.1", MON), 5)
        s.settimeout(5)
        time.sleep(0.5)
        try:
            s.recv(8192)
        except Exception:
            pass
        s.sendall(("screendump %s\n" % ppm).encode())
        time.sleep(4)
        try:
            s.recv(8192)
        except Exception:
            pass
        s.close()
        if not os.path.exists(ppm):
            print("%-22s no screenshot" % label)
            return
        from PIL import Image
        im = Image.open(ppm)
        cols = im.convert("RGB").getcolors(maxcolors=200000) or []
        graphics = im.size != (720, 400)
        print("%-22s %-9s colours=%-6d %s" % (
            label, "%dx%d" % im.size, len(cols),
            "GRAPHICS MODE" if graphics else "text only"))
        im.save("/tmp/base_%s.png" % label.replace("/", "_"))
    finally:
        proc.kill()
        if os.path.exists(ppm):
            os.remove(ppm)
        time.sleep(1)


if __name__ == "__main__":
    iso = sys.argv[1]
    probe(iso, ["-display", "none"], "display-none")
    probe(iso, ["-vnc", ":97"], "vnc")
    probe(iso, ["-display", "egl-headless"], "egl-headless")
    probe(iso, ["-display", "none", "-vga", "cirrus"], "none-cirrus")
    probe(iso, ["-vnc", ":96", "-vga", "std"], "vnc-std")