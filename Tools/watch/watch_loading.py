"""
Read-only watcher for the 36731 client's loading-screen gate.

Attaches with PROCESS_VM_READ only - no debugger, no breakpoints, no suspension - because the
client crashes when paused. Prints a line only when a watched value changes.

    python watch_loading.py [poll_ms]

Addresses: SkySagaU.exe 36731, no ASLR. See docs/client/loading-screen.md.
"""

import ctypes
import ctypes.wintypes as wt
import struct
import sys
import time
import datetime

PROCESS_NAME = "SkySaga.exe"

MODE_GLOBAL_PTR = 0x0143785C   # DAT_0143785C, ModeGlobal singleton
GAME_FLAGS_PTR = 0x01440E98    # DAT_01440E98, +0x70 = "offline/editor" byte read by gameplay tick
VT_LOADING_SCREEN = 0x012E8BFC  # ui_loading_screen
VT_PORTALFADE = 0x012E444C      # ui_portalfade, UI screen 0x42
VT_PLAYER = 0x012D2344          # local-player entity class (fade requester, FUN_0086B9C0)

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
PROCESS_QUERY_INFORMATION = 0x0400
PROCESS_VM_READ = 0x0010
TH32CS_SNAPPROCESS = 0x2
MEM_COMMIT = 0x1000
MEM_PRIVATE = 0x20000
PAGE_READWRITE = 0x04


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("cntUsage", wt.DWORD), ("th32ProcessID", wt.DWORD),
                ("th32DefaultHeapID", ctypes.c_void_p), ("th32ModuleID", wt.DWORD),
                ("cntThreads", wt.DWORD), ("th32ParentProcessID", wt.DWORD),
                ("pcPriClassBase", ctypes.c_long), ("dwFlags", wt.DWORD),
                ("szExeFile", ctypes.c_wchar * 260)]


class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", wt.DWORD), ("RegionSize", ctypes.c_size_t),
                ("State", wt.DWORD), ("Protect", wt.DWORD), ("Type", wt.DWORD)]


def find_pid(name):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    ok = k32.Process32FirstW(snap, ctypes.byref(entry))
    pid = None
    while ok:
        if entry.szExeFile.lower() == name.lower():
            pid = entry.th32ProcessID
        ok = k32.Process32NextW(snap, ctypes.byref(entry))
    k32.CloseHandle(snap)
    return pid


class Proc:
    def __init__(self, pid):
        self.pid = pid
        self.h = k32.OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, pid)
        if not self.h:
            raise OSError(ctypes.get_last_error())

    def read(self, addr, size):
        buf = ctypes.create_string_buffer(size)
        got = ctypes.c_size_t()
        if not k32.ReadProcessMemory(self.h, ctypes.c_void_p(addr), buf, size, ctypes.byref(got)):
            return None
        return buf.raw[:got.value]

    def u8(self, a):
        b = self.read(a, 1)
        return b[0] if b else None

    def u16(self, a):
        b = self.read(a, 2)
        return struct.unpack("<H", b)[0] if b and len(b) == 2 else None

    def u32(self, a):
        b = self.read(a, 4)
        return struct.unpack("<I", b)[0] if b and len(b) == 4 else None

    def alive(self):
        code = wt.DWORD()
        k32.GetExitCodeProcess(self.h, ctypes.byref(code))
        return code.value == 259  # STILL_ACTIVE

    def scan_vtables(self, vtables):
        """Find heap objects whose first dword is one of `vtables`. Private RW memory only."""
        needles = {v: struct.pack("<I", v) for v in vtables}
        found = {v: [] for v in vtables}
        addr = 0x10000
        mbi = MBI()
        while addr < 0x7FFF0000:
            if not k32.VirtualQueryEx(self.h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
                break
            base = mbi.BaseAddress or 0
            size = mbi.RegionSize
            # Low byte only: the client's big heap regions carry PAGE_WRITECOMBINE (0x404) too.
            # Read in chunks - one region outgrew 256 MB mid-run and the objects "vanished".
            if mbi.State == MEM_COMMIT and mbi.Type == MEM_PRIVATE and (mbi.Protect & 0xFF) == PAGE_READWRITE:
                chunk = 64 * 1024 * 1024
                for off in range(0, size, chunk):
                    data = self.read(base + off, min(chunk, size - off))
                    if not data:
                        continue
                    for v, n in needles.items():
                        i = data.find(n)
                        while i != -1:
                            if i % 4 == 0:
                                found[v].append(base + off + i)
                            i = data.find(n, i + 4)
            addr = base + size
        return found


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


def snapshot(p, ls_objs, gp_objs):
    s = {}
    mg = p.u32(MODE_GLOBAL_PTR)
    s["MG"] = f"{mg:#x}" if mg else mg
    if mg:
        spec = mg + 0xFC4
        w, h, fmt = p.u16(spec + 0x2E), p.u16(spec + 0x30), p.u8(spec + 0x0C)
        s["FC4"] = f"{w}x{h} fmt={fmt} {'VALID' if (w and h and fmt != 3) else 'invalid'}"
        s["state(+D34)"] = p.u8(mg + 0xD34)
        s["state2(+D8C)"] = p.u8(mg + 0xD38 + 0x54)
        s["MG+1044"] = p.u8(mg + 0x1044)
    gf = p.u32(GAME_FLAGS_PTR)
    if gf:
        s["G+70"] = p.u8(gf + 0x70)
    for i, o in enumerate(ls_objs):
        if p.u32(o) != VT_LOADING_SCREEN:
            s[f"LS{i}"] = "gone"
            continue
        s[f"LS{i}"] = (f"@{o:#x} gate194={p.u8(o + 0x194)} fsm17C={p.u32(o + 0x17C)} "
                       f"f160={p.u8(o + 0x160)} f1C0={p.u8(o + 0x1C0)} n1B0={p.u32(o + 0x1B0)}")
    for i, o in enumerate(gp_objs):
        vt = p.u32(o)
        if vt == VT_PORTALFADE:
            s[f"PF{i}"] = (f"@{o:#x} enter138={p.u8(o + 0x138)} leave139={p.u8(o + 0x139)} "
                           f"f13a={p.u8(o + 0x13A)}")
        elif vt == VT_PLAYER:
            s[f"PL{i}"] = (f"@{o:#x} flags81={p.u8(o + 0x81):#04x} req91={p.u8(o + 0x91)} "
                           f"leave92={p.u8(o + 0x92)} cancel93={p.u8(o + 0x93)} tpmethod50={p.u8(o + 0x50)}")
        else:
            s[f"OB{i}"] = "gone"
    return s


def main():
    poll = (int(sys.argv[1]) if len(sys.argv) > 1 else 100) / 1000.0
    print(f"[{ts()}] waiting for {PROCESS_NAME} ...", flush=True)
    while True:
        pid = find_pid(PROCESS_NAME)
        if not pid:
            time.sleep(0.5)
            continue
        p = Proc(pid)
        print(f"[{ts()}] attached read-only to pid {pid}", flush=True)
        prev = {}
        ls_objs, gp_objs = [], []
        last_scan = 0.0
        while p.alive():
            now = time.time()
            stale = any(p.u32(o) != VT_LOADING_SCREEN for o in ls_objs) or \
                any(p.u32(o) not in (VT_PORTALFADE, VT_PLAYER) for o in gp_objs)
            if now - last_scan > (3.0 if (not ls_objs or not gp_objs or stale) else 15.0):
                found = p.scan_vtables([VT_LOADING_SCREEN, VT_PORTALFADE, VT_PLAYER])
                new_gp = found[VT_PORTALFADE] + found[VT_PLAYER]
                if found[VT_LOADING_SCREEN] != ls_objs or new_gp != gp_objs:
                    ls_objs, gp_objs = found[VT_LOADING_SCREEN], new_gp
                    print(f"[{ts()}] objects: loading_screen={[hex(x) for x in ls_objs]} "
                          f"portalfade={[hex(x) for x in found[VT_PORTALFADE]]} "
                          f"player={[hex(x) for x in found[VT_PLAYER]]}", flush=True)
                    prev = {k: v for k, v in prev.items() if not k.startswith(("LS", "PF", "PL", "OB"))}
                last_scan = now
            cur = snapshot(p, ls_objs, gp_objs)
            changed = {k: v for k, v in cur.items() if prev.get(k) != v}
            if changed:
                print(f"[{ts()}] " + "  ".join(f"{k}={v}" for k, v in changed.items()), flush=True)
                prev.update(cur)
            time.sleep(poll)
        print(f"[{ts()}] process {pid} exited", flush=True)
        k32.CloseHandle(p.h)
        # The dead process lingers in the snapshot for a moment; don't re-attach to it.
        while find_pid(PROCESS_NAME) == pid:
            time.sleep(0.5)


if __name__ == "__main__":
    main()
