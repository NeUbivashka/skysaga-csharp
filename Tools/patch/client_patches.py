"""
Runtime patches for the 36731 client (SkySaga.exe, packed; no ASLR).

The exe is packed, so patches cannot go into the file. This waits for SkySaga.exe, waits until
the unpacked code at each patch site matches the expected original bytes, then writes the patch
with WriteProcessMemory. No debugger and no suspension, because the client dies when paused.

    python client_patches.py          # patch the next SkySaga.exe that starts, then exit
    python client_patches.py --loop   # keep patching every new SkySaga.exe

Every patch is guarded by its original bytes. If they don't match (another build, already
patched), it is skipped and a line says so.
"""

import ctypes
import ctypes.wintypes as wt
import datetime
import sys
import time

PROCESS_NAME = "SkySaga.exe"

PATCHES = [
    {
        # STATUS_FLOAT_MULTIPLE_TRAPS at 0x00D45EDC, intermittent, ~2 s after ClientReadyToPlay
        # (READY_TO_PLAY). FUN_00D45E60 divides by the timer duration at [timer+0x38]. Its other
        # caller FUN_00D459B0 checks for <= 0 first; this one, FUN_00BE0540, does not, and it is
        # only reached from virtualized code, so the timer with the zero duration can't be traced
        # statically. Same check as the guarded caller: duration <= 0 returns 0.0.
        #
        #   cmp byte [ebp+8],0 / jz ret0 / cmp dword [eax+0x38],0 / jle ret0 /
        #   mov ecx,eax / call 0x00D45E60 / pop ebp / ret 4 / ret0: fldz / pop ebp / ret 4
        #
        # Integer compare on the float: positive floats are positive ints; -x and 0.0 are <= 0.
        "name": "guard zero duration in FUN_00BE0540",
        "address": 0x00BE0552,
        "original": bytes.fromhex("807d0800750ec7450800000000d945085dc204008bc8e8f35816005dc20400"),
        "patch": bytes.fromhex("807d08007411837838007e0b8bc8e8fb5816005dc20400d9ee5dc20400cccc"),
    },
]

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
PROCESS_ALL_NEEDED = 0x0008 | 0x0010 | 0x0020 | 0x0400  # VM_OPERATION | VM_READ | VM_WRITE | QUERY_INFORMATION
PAGE_EXECUTE_READWRITE = 0x40
TH32CS_SNAPPROCESS = 0x2


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("cntUsage", wt.DWORD), ("th32ProcessID", wt.DWORD),
                ("th32DefaultHeapID", ctypes.c_void_p), ("th32ModuleID", wt.DWORD),
                ("cntThreads", wt.DWORD), ("th32ParentProcessID", wt.DWORD),
                ("pcPriClassBase", ctypes.c_long), ("dwFlags", wt.DWORD),
                ("szExeFile", ctypes.c_wchar * 260)]


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


def find_pids(name):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    pids = []
    ok = k32.Process32FirstW(snap, ctypes.byref(entry))
    while ok:
        if entry.szExeFile.lower() == name.lower():
            pids.append(entry.th32ProcessID)
        ok = k32.Process32NextW(snap, ctypes.byref(entry))
    k32.CloseHandle(snap)
    return pids


def read(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    got = ctypes.c_size_t()
    if not k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(got)):
        return None
    return buf.raw[:got.value]


def write_code(h, addr, data):
    old = wt.DWORD()
    if not k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), PAGE_EXECUTE_READWRITE, ctypes.byref(old)):
        return False
    n = ctypes.c_size_t()
    ok = k32.WriteProcessMemory(h, ctypes.c_void_p(addr), data, len(data), ctypes.byref(n))
    k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), old.value, ctypes.byref(wt.DWORD()))
    k32.FlushInstructionCache(h, ctypes.c_void_p(addr), len(data))
    return bool(ok) and n.value == len(data)


def alive(h):
    code = wt.DWORD()
    k32.GetExitCodeProcess(h, ctypes.byref(code))
    return code.value == 259


def patch_process(pid, timeout=60.0):
    h = k32.OpenProcess(PROCESS_ALL_NEEDED, False, pid)
    if not h:
        print(f"[{ts()}] pid {pid}: OpenProcess failed ({ctypes.get_last_error()})", flush=True)
        return
    pending = list(PATCHES)
    deadline = time.time() + timeout
    while pending and time.time() < deadline and alive(h):
        for p in list(pending):
            cur = read(h, p["address"], len(p["original"]))
            if cur == p["patch"]:
                print(f"[{ts()}] pid {pid}: already patched: {p['name']}", flush=True)
                pending.remove(p)
            elif cur == p["original"]:
                ok = write_code(h, p["address"], p["patch"])
                print(f"[{ts()}] pid {pid}: {'patched' if ok else 'WRITE FAILED'}: {p['name']}", flush=True)
                pending.remove(p)
        time.sleep(0.05)  # code is still packed until the bytes match
    for p in pending:
        print(f"[{ts()}] pid {pid}: skipped (bytes never matched): {p['name']}", flush=True)
    k32.CloseHandle(h)


def main():
    loop = "--loop" in sys.argv
    done = set()
    print(f"[{ts()}] waiting for {PROCESS_NAME} ...", flush=True)
    while True:
        for pid in find_pids(PROCESS_NAME):
            if pid not in done:
                done.add(pid)
                patch_process(pid)
                if not loop:
                    return
        time.sleep(0.2)


if __name__ == "__main__":
    main()
