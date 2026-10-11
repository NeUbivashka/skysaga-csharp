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

def _rel32(src_next, dst):
    return ((dst - src_next) & 0xFFFFFFFF).to_bytes(4, "little")


def _d45e60_cave(cave):
    # mov eax,[ecx+0x34] / add eax,eax / jz ret0        ; [timer+0x34] is +0.0 or -0.0
    # cmp dword [ecx+0x38],0 / jle ret0                 ; duration <= 0
    # push ebp / mov ebp,esp / sub esp,8 / jmp 0x00D45E66   (stolen prologue, then the original)
    # ret0: fldz / ret
    return (bytes.fromhex("8b4134" "03c0" "7411" "83793800" "7e0b" "55" "8bec" "83ec08" "e9")
            + _rel32(cave + 24, 0x00D45E66) + bytes.fromhex("d9ee" "c3"))


PATCHES = [
    {
        # STATUS_FLOAT_MULTIPLE_TRAPS at 0x00D45EDC, intermittent, a few seconds after
        # ClientReadyToPlay. FUN_00D45E60 (__fastcall, ecx = timer) ends with
        #     DIVSS xmm1, [ebp-8]      ; [ebp-8] = [timer+0x34] * k / [timer+0x38]
        # and that divisor is zero when [timer+0x34] is zero. (The first division, by +0x38, at
        # 0x00D45E86 does not trap, so +0x38 is not the zero.) Its callers include virtualized code
        # that no xref shows, so the guard sits at the function entry, in a cave allocated in the
        # client. Either field zero / non-positive returns 0.0.
        #
        # Superseded an earlier patch in FUN_00BE0540 that checked only +0x38 at one caller: the
        # client still crashed with it applied (pid 8708, 17:53).
        "name": "guard zero divisor in FUN_00D45E60",
        "address": 0x00D45E60,
        "original": bytes.fromhex("558bec83ec08"),
        "cave": _d45e60_cave,
        "patch": lambda cave: b"\xe9" + _rel32(0x00D45E65, cave) + b"\x90",
    },
]

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
PROCESS_ALL_NEEDED = 0x0008 | 0x0010 | 0x0020 | 0x0400  # VM_OPERATION | VM_READ | VM_WRITE | QUERY_INFORMATION
PAGE_EXECUTE_READWRITE = 0x40
MEM_COMMIT = 0x1000
MEM_RESERVE = 0x2000
k32.VirtualAllocEx.restype = ctypes.c_void_p
k32.VirtualAllocEx.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_size_t, wt.DWORD, wt.DWORD]
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
            if cur is None or cur != p["original"]:
                # Still packed, or already patched by an earlier run (a static patch matches its
                # bytes exactly; a cave patch starts with the jmp into the cave).
                if cur is not None and ((not callable(p["patch"]) and cur == p["patch"])
                                        or (callable(p["patch"]) and cur[:1] == b"\xe9")):
                    print(f"[{ts()}] pid {pid}: already patched: {p['name']}", flush=True)
                    pending.remove(p)
                continue
            if "cave" in p:
                cave = k32.VirtualAllocEx(h, None, 0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE)
                if not cave:
                    print(f"[{ts()}] pid {pid}: VirtualAllocEx failed ({ctypes.get_last_error()}): {p['name']}", flush=True)
                    pending.remove(p)
                    continue
                cave_bytes = p["cave"](cave)
                if not write_code(h, cave, cave_bytes):
                    print(f"[{ts()}] pid {pid}: cave write failed: {p['name']}", flush=True)
                    pending.remove(p)
                    continue
                patch = p["patch"](cave)
            else:
                patch = p["patch"]
            ok = write_code(h, p["address"], patch)
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
