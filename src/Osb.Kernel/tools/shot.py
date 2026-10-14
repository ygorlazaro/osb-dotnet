#!/usr/bin/env python3
"""Boot OSB in QEMU, drive the serial console, and grab a screenshot.

Usage:
  shot.py OUT.png "serial commands" [wait_seconds] [shot_after_seconds]

The commands are fed to the guest serial line, exactly like typing at the
OSB prompt. A QEMU monitor on a TCP port is used for `screendump`, so the
captured image is whatever the VGA device is actually displaying - the only
way to check a graphical UI from here.
"""
import os
import socket
import subprocess
import sys
import time

KERNEL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PORT = 4599


def monitor(cmd, retries=20):
    for _ in range(retries):
        try:
            s = socket.create_connection(("127.0.0.1", PORT), 3)
            s.settimeout(4)
            time.sleep(0.4)
            try:
                s.recv(8192)
            except Exception:
                pass
            s.sendall(cmd.encode() + b"\n")
            time.sleep(1.5)
            try:
                s.recv(8192)
            except Exception:
                pass
            s.close()
            return True
        except Exception:
            time.sleep(0.5)
    return False


def ppm_to_png(src, dst):
    from PIL import Image
    im = Image.open(src)
    im.save(dst)
    return im.size


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "/tmp/osb.png"
    cmds = sys.argv[2] if len(sys.argv) > 2 else ""
    hold = float(sys.argv[3]) if len(sys.argv) > 3 else 60.0
    shot_at = float(sys.argv[4]) if len(sys.argv) > 4 else 20.0

    ppm = "/tmp/_osb_shot.ppm"
    for p in (ppm, out):
        if os.path.exists(p):
            os.remove(p)

    qemu = [
        "qemu-system-x86_64",
        "-cdrom", os.path.join(KERNEL, "osb.iso"),
        "-m", "128M",
        "-drive", "file=%s,if=ide,index=0" % os.path.join(KERNEL, "osb.img"),
        # A graphical console is required, not just any display: with
        # "-display none" QEMU builds a text-only surface and the VGA is
        # never scanned out, so graphics mode looks like it did nothing.
        # VNC is graphical but still headless.
        "-display", "egl-headless",
        "-serial", "stdio",
        "-no-reboot",
        "-monitor", "tcp:127.0.0.1:%d,server,nowait" % PORT,
    ]
    log = open("/tmp/_osb_qemu.log", "wb")
    proc = subprocess.Popen(qemu, stdin=subprocess.PIPE, stdout=log,
                            stderr=subprocess.STDOUT)
    start = time.time()
    try:
        # Wait for the guest to be far enough along to accept input.
        time.sleep(min(16.0, shot_at))
        if cmds:
            proc.stdin.write(cmds.encode())
            proc.stdin.flush()
        time.sleep(max(0.0, shot_at - 16.0))

        if not monitor("screendump %s" % ppm):
            print("monitor: could not reach QEMU")
        else:
            time.sleep(1.0)
            if os.path.exists(ppm):
                size = ppm_to_png(ppm, out)
                print("screenshot %s %dx%d" % (out, size[0], size[1]))
            else:
                print("screendump produced nothing")
        time.sleep(2.0)
        try:
            proc.stdin.write(b"POWEROFF\n")
            proc.stdin.flush()
        except Exception:
            pass
    finally:
        time.sleep(2.0)
        if proc.poll() is None:
            proc.kill()
        log.close()
    print("--- serial ---")
    try:
        data = open("/tmp/_osb_qemu.log", "rb").read().decode(errors="replace")
        print(data.replace("\r", "")[-2000:])
    except Exception:
        pass


if __name__ == "__main__":
    main()