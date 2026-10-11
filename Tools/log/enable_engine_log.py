"""
Turn on the 36731 client's own engine log, written to <Client>\\engine.log.

Logging is not stripped from the release build. Every call ends in FUN_00B40750(channel, level,
category, ...), which writes through a channel object - but no channel is ever created (the vector
DAT_01487860..64 is empty and the channel index globals DAT_01404150..64 are all -1), and the
level gate DAT_01404120 is 0. So every message is dropped.

This makes one channel the way the client itself would: a remote thread calls FUN_006E10B0
(cdecl: object, path, flags, bufsize) on memory allocated in the client, so the client's own CRT
opens the file. The tool then installs that channel as the only vector entry, points every channel
index at entry 0, and raises the level gate. No debugger and no suspension.

Exactly one entry, never copies of it. On exit the engine closes every channel (FUN_00B40D10 ->
FUN_006E1E20: flush, free the buffer, fclose). Copies share the buffer and FILE*, so the second close
freed the same block again and the allocator's header check killed the client ("Force crash: FTL",
write to 0xF41 at 0x006E6461). The vector's capacity word (DAT_01487858) stays 0, so the engine
never tries to free the vector itself, which lives in VirtualAllocEx memory. Log_Write bounds-checks
the channel index, so the one entry is safe.

    python enable_engine_log.py            # wait for SkySaga.exe, enable, exit
    python enable_engine_log.py --loop     # do it for every new SkySaga.exe

Layouts (0x424-byte channel object): path char[0x410] | flags +0x410 | FILE* +0x414 |
buffer +0x418 | buffer cap +0x41C | buffer len +0x420.
"""

import ctypes
import ctypes.wintypes as wt
import datetime
import struct
import sys
import time

PROCESS_NAME = "SkySaga.exe"
LOG_PATH = rb"D:\codya\SkySaga\Alpha v10 36731 fixed\Client\engine.log"

FN_OPEN_CHANNEL = 0x006E10B0   # FUN_006E10B0(obj, path, flags, bufsize) -> obj or 0
CHANNEL_FLAGS = 0x184          # messageLog flags (0x104) + 0x80: flush after every write
CHANNEL_SIZE = 0x424
CHANNEL_COUNT = 1             # see the docstring: copies double-free on exit
VEC_BEGIN, VEC_END = 0x01487860, 0x01487864
CHANNEL_INDEX_GLOBALS = range(0x01404150, 0x01404168, 4)
LEVEL_GATE = 0x01404120        # messages with level <= this are written; 8 = everything
MODE_GLOBAL_PTR = 0x0143785C   # non-zero once the client is initialized

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
k32.OpenProcess.restype = wt.HANDLE
k32.VirtualAllocEx.restype = ctypes.c_void_p
k32.VirtualAllocEx.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_size_t, wt.DWORD, wt.DWORD]
k32.CreateRemoteThread.restype = wt.HANDLE
k32.CreateRemoteThread.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p,
                                   ctypes.c_void_p, wt.DWORD, ctypes.c_void_p]
k32.ReadProcessMemory.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p]
k32.WriteProcessMemory.argtypes = [wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p]

ACCESS = 0x0002 | 0x0008 | 0x0010 | 0x0020 | 0x0400  # CREATE_THREAD|VM_OPERATION|VM_READ|VM_WRITE|QUERY_INFO


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


def find_pids():
    sys.path.insert(0, r"D:/codya/SkySaga/Server_alpha_10/Tools/patch")
    import client_patches
    return client_patches.find_pids(PROCESS_NAME)


def read(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if not k32.ReadProcessMemory(h, addr, buf, size, ctypes.byref(n)):
        return None
    return buf.raw[:n.value]


def write(h, addr, data):
    n = ctypes.c_size_t()
    return bool(k32.WriteProcessMemory(h, addr, data, len(data), ctypes.byref(n)))


def u32(h, addr):
    b = read(h, addr, 4)
    return struct.unpack("<I", b)[0] if b and len(b) == 4 else None


def enable(pid):
    h = k32.OpenProcess(ACCESS, False, pid)
    if not h:
        print(f"[{ts()}] pid {pid}: OpenProcess failed ({ctypes.get_last_error()})", flush=True)
        return
    # Wait until the client is initialized (heap and CRT up, code unpacked).
    for _ in range(600):
        if u32(h, MODE_GLOBAL_PTR):
            break
        time.sleep(0.05)
    else:
        print(f"[{ts()}] pid {pid}: client never initialized", flush=True)
        return

    block = k32.VirtualAllocEx(h, None, 0x1000 + CHANNEL_SIZE * CHANNEL_COUNT, 0x3000, 0x40)
    path_addr, stub_addr, vec_addr = block, block + 0x400, block + 0x1000
    write(h, path_addr, LOG_PATH + b"\0")

    # push 0 / push flags / push path / push channel0 / mov eax,FN / call eax / add esp,16 /
    # xor eax,eax / ret 4
    stub = (b"\x6a\x00" + b"\x68" + struct.pack("<I", CHANNEL_FLAGS)
            + b"\x68" + struct.pack("<I", path_addr) + b"\x68" + struct.pack("<I", vec_addr)
            + b"\xb8" + struct.pack("<I", FN_OPEN_CHANNEL) + b"\xff\xd0" + b"\x83\xc4\x10"
            + b"\x31\xc0" + b"\xc2\x04\x00")
    write(h, stub_addr, stub)

    th = k32.CreateRemoteThread(h, None, 0, stub_addr, None, 0, None)
    if not th:
        print(f"[{ts()}] pid {pid}: CreateRemoteThread failed ({ctypes.get_last_error()})", flush=True)
        return
    k32.WaitForSingleObject(th, 5000)
    k32.CloseHandle(th)

    channel = read(h, vec_addr, CHANNEL_SIZE)
    file_ptr = struct.unpack_from("<I", channel, 0x414)[0]
    if not file_ptr:
        print(f"[{ts()}] pid {pid}: channel did not open {LOG_PATH!r}", flush=True)
        return
    write(h, VEC_BEGIN, struct.pack("<II", vec_addr, vec_addr + CHANNEL_SIZE * CHANNEL_COUNT))
    for g in CHANNEL_INDEX_GLOBALS:
        write(h, g, struct.pack("<I", 0))
    write(h, LEVEL_GATE, struct.pack("<I", 8))
    print(f"[{ts()}] pid {pid}: engine log on -> {LOG_PATH.decode()} (FILE* {file_ptr:#x})", flush=True)
    k32.CloseHandle(h)


def main():
    loop = "--loop" in sys.argv
    done = set()
    print(f"[{ts()}] waiting for {PROCESS_NAME} ...", flush=True)
    while True:
        for pid in find_pids():
            if pid not in done:
                done.add(pid)
                enable(pid)
                if not loop:
                    return
        time.sleep(0.2)


if __name__ == "__main__":
    main()
