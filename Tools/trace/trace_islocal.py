"""
Trace who sets and clears Player.islocal in the 36731 client - without a debugger.

Each hook is a jmp at a function entry into a cave that appends one 128-byte record to a ring
buffer allocated in the client, then runs the stolen prologue and jumps back. No suspension, no
breakpoints. This script polls the ring and prints the records.

Record: tag | tick (KUSER_SHARED_DATA TickCountLow) | ecx | 10 dwords of the stack at entry
(return address, args...) | extra | 6 return addresses from the EBP chain.

    python trace_islocal.py            # attach to the next SkySaga.exe and trace until it exits

Hooks (all 36731 addresses):
  SYNC_RX   FUN_007D4C80  RPCEntitySync handler (extra = entity id)
  APPLY     FUN_00B43760  entity sync apply (ecx = entity)
  READ17    FUN_00C75410  clientplayeraspects binding read, only binding 0x11 = islocal
            (extra = bindings byte +0x61 before the read; bit 3 = islocal)
  SET17     FUN_00C77260  islocal setter behind SetParameter (extra = new<<8 | old +0x61)
  DTOR      FUN_0086C190  player aspects component destructor
  TOD_READ  FUN_00D462E0  clienttimeofdaycomponent binding read (binding, stream bit offset)

    python trace_islocal.py --hooks TOD_READ,SYNC_RX   # only these hooks
"""

import ctypes
import ctypes.wintypes as wt
import datetime
import struct
import sys
import time

sys.path.insert(0, r"D:/codya/SkySaga/Server_alpha_10/Tools/patch")
import client_patches as cp  # noqa: E402

RING_ENTRIES = 1024
ENTRY = 128
HEADER = 0x40


def rel32(src_next, dst):
    return struct.pack("<I", (dst - src_next) & 0xFFFFFFFF)


def build_cave(cave, ring, tag, stolen, resume, filter_binding=None, extra=b""):
    """Assemble one logging cave. `extra` leaves the value to record in eax (esi = entry esp)."""
    c = bytearray()
    c += b"\x60\x9c"                       # pushad / pushfd
    c += b"\xfc"                           # cld
    c += b"\x8d\x74\x24\x24"               # lea esi,[esp+0x24]   ; esp at entry
    jne_at = None
    if filter_binding is not None:
        c += b"\x83\x7e\x04" + bytes([filter_binding])  # cmp dword [esi+4],imm8
        c += b"\x0f\x85\x00\x00\x00\x00"   # jne done (patched below)
        jne_at = len(c)
    c += b"\xb8\x01\x00\x00\x00"           # mov eax,1
    c += b"\xbf" + struct.pack("<I", ring)  # mov edi,ring
    c += b"\xf0\x0f\xc1\x07"               # lock xadd [edi],eax
    c += b"\x25" + struct.pack("<I", RING_ENTRIES - 1)  # and eax,mask
    c += b"\xc1\xe0\x07"                   # shl eax,7
    c += b"\x8d\xbc\x07" + struct.pack("<I", HEADER)  # lea edi,[edi+eax+HEADER]
    c += b"\xc7\x07" + struct.pack("<I", tag)  # mov dword [edi],tag
    c += b"\xa1\x20\x03\xfe\x7f"           # mov eax,[0x7ffe0320]
    c += b"\x89\x47\x04"                   # mov [edi+4],eax
    c += b"\x8b\x44\x24\x1c"               # mov eax,[esp+0x1c]   ; saved ecx
    c += b"\x89\x47\x08"                   # mov [edi+8],eax
    c += b"\x57\x56"                       # push edi / push esi
    c += b"\x83\xc7\x0c"                   # add edi,12
    c += b"\xb9\x0a\x00\x00\x00"           # mov ecx,10
    c += b"\xf3\xa5"                       # rep movsd
    c += b"\x5e\x5f"                       # pop esi / pop edi
    c += b"\x31\xc0"                       # xor eax,eax
    c += extra
    c += b"\x89\x47\x34"                   # mov [edi+0x34],eax
    c += b"\x31\xc0"                       # xor eax,eax  ; clear the chain slots (ring reuse)
    for off in range(0x38, 0x50, 4):
        c += b"\x89\x47" + bytes([off])    # mov [edi+off],eax
    # EBP chain: 6 return addresses, only while ebp stays inside [esi, esi+1MB).
    c += b"\x8b\x5c\x24\x0c"               # mov ebx,[esp+0xc]    ; saved ebp
    c += b"\x8d\x57\x38"                   # lea edx,[edi+0x38]
    c += b"\xb9\x06\x00\x00\x00"           # mov ecx,6
    loop = len(c)
    c += b"\x39\xf3"                       # cmp ebx,esi
    c += b"\x72\x00"; jb1 = len(c)         # jb end
    c += b"\x89\xd8"                       # mov eax,ebx
    c += b"\x29\xf0"                       # sub eax,esi
    c += b"\x3d\x00\x00\x10\x00"           # cmp eax,0x100000
    c += b"\x73\x00"; jae1 = len(c)        # jae end
    c += b"\x8b\x43\x04"                   # mov eax,[ebx+4]
    c += b"\x89\x02"                       # mov [edx],eax
    c += b"\x83\xc2\x04"                   # add edx,4
    c += b"\x8b\x1b"                       # mov ebx,[ebx]
    c += b"\xe2" + bytes([(loop - (len(c) + 2)) & 0xFF])  # loop
    end = len(c)
    c[jb1 - 1] = end - jb1
    c[jae1 - 1] = end - jae1
    done = len(c)
    if jne_at is not None:
        c[jne_at - 4:jne_at] = struct.pack("<i", done - jne_at)
    c += b"\x9d\x61"                       # popfd / popad
    c += stolen
    c += b"\xe9" + rel32(cave + len(c) + 5, resume)
    return bytes(c)


# extra snippets (esi = esp at entry, [esp+0x1c] = saved ecx)
X_ENTITY_ID = b"\x8b\x46\x08\x8b\x00"                       # mov eax,[esi+8] / mov eax,[eax]
X_FLAGS61 = b"\x8b\x44\x24\x1c\x0f\xb6\x40\x61"             # mov eax,[esp+1c] / movzx eax,byte[eax+61]
X_SET17 = (b"\x8b\x46\x04\x0f\xb6\x00\xc1\xe0\x08"          # mov eax,[esi+4] / movzx eax,[eax] / shl eax,8
           b"\x8b\x4c\x24\x1c\x0f\xb6\x49\x61\x09\xc8")     # mov ecx,[esp+1c] / movzx ecx,[ecx+61] / or eax,ecx

HOOKS = [
    # name, tag, address, original (stolen) bytes, filter, extra
    ("SYNC_RX", 1, 0x007D4C80, bytes.fromhex("558bec51f6411c20"), None, X_ENTITY_ID),
    ("APPLY", 2, 0x00B43760, bytes.fromhex("558bec81ecf00a0000"), None, bytes.fromhex("8b4e048b01c1e0100b4108")),
    ("READ17", 3, 0x00C75410, bytes.fromhex("558bec83ec2c"), 0x11, X_FLAGS61),
    ("SET17", 4, 0x00C77260, bytes.fromhex("558bec8b4508"), None, X_SET17),
    ("DTOR", 5, 0x0086C190, bytes.fromhex("558bec568bf1"), None, b""),
    # clienttimeofdaycomponent bindings read (switch on binding); extra = stream read offset (bits)
    ("TOD_READ", 6, 0x00D462E0, bytes.fromhex("558bec8b5508"), None, bytes.fromhex("8b46088b4008")),
    # Inside Entity_ApplySyncEx's loop. LOOP_BIT: mask bit set for index EDI;
    # extra = index | (mask word 0 << 16)  (saved edi = [esp+4], saved ebp = [esp+0xc]).
    # EntityAdd receive stub right after the sync-data length read; extra = readpos << 16 | length.
    ("ADD_LEN", 9, 0x007F121C, bytes.fromhex("8d8de8feffff"), None,
     bytes.fromhex("8b44240c8b80bcf9ffff8b4c24148b4908c1e11009c8")),
    # 5th stack argument and its first dword (BitStream bits used) at three steps of EntityAdd.
    ("H_ADD", 10, 0x007D4BD0, bytes.fromhex("558bec5153"), None, bytes.fromhex("8b46148b00")),
    ("H_CREATE", 11, 0x00BE3880, bytes.fromhex("558bec83ec08"), None, bytes.fromhex("8b46148b00")),
    ("H_DC", 12, 0x00DC4440, bytes.fromhex("558bec5153"), None, bytes.fromhex("8b46148b00")),
    # EntityAdd stub right after the copy; extra = rpc stream bits << 16 | copied bits.
    ("ADD_COPY", 13, 0x007F122E, bytes.fromhex("6a00ff368d85e8feffff"), None,
     bytes.fromhex("8b4424148b00c1e0108b4c240c0b81e8feffff")),
    ("LOOP_BIT", 7, 0x00B4388E, bytes.fromhex("8b8d9cfdffff"), None,
     bytes.fromhex("8b4424048b4c240c8b49e0c1e11009c8")),
    # COMP_FOUND: component for the parameter found (esi); extra = component.
    ("COMP_FOUND", 8, 0x00B438F8, bytes.fromhex("8b068bce8b4008"), None, bytes.fromhex("8b442408")),
]
TAGS = {h[1]: h[0] for h in HOOKS}


def ts():
    return datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]


def selected_hooks():
    if "--hooks" in sys.argv:
        names = set(sys.argv[sys.argv.index("--hooks") + 1].split(","))
        return [x for x in HOOKS if x[0] in names]
    return [x for x in HOOKS if x[0] != "TOD_READ"]


def install(h):
    ring = cp.k32.VirtualAllocEx(h, None, HEADER + RING_ENTRIES * ENTRY, 0x3000, 0x04)
    caves = cp.k32.VirtualAllocEx(h, None, 0x1000, 0x3000, 0x40)
    if not ring or not caves:
        raise RuntimeError(f"VirtualAllocEx failed ({ctypes.get_last_error()})")
    off = 0
    for name, tag, addr, stolen, flt, extra in selected_hooks():
        cave = caves + off
        code = build_cave(cave, ring, tag, stolen, addr + len(stolen), flt, extra)
        off += (len(code) + 15) & ~15
        if not cp.write_code(h, cave, code):
            raise RuntimeError(f"cave write failed: {name}")
        jmp = b"\xe9" + rel32(addr + 5, cave) + b"\x90" * (len(stolen) - 5)
        if not cp.write_code(h, addr, jmp):
            raise RuntimeError(f"hook write failed: {name}")
        print(f"[{ts()}] hooked {name} @ {addr:#010x} -> cave {cave:#x}", flush=True)
    return ring


def wait_unpacked(h):
    while cp.alive(h):
        if all(cp.read(h, a, len(o)) == o for _, _, a, o, _, _ in selected_hooks()):
            return True
        time.sleep(0.05)
    return False


def main():
    print(f"[{ts()}] waiting for {cp.PROCESS_NAME} ...", flush=True)
    while not (pids := cp.find_pids(cp.PROCESS_NAME)):
        time.sleep(0.2)
    pid = pids[0]
    h = cp.k32.OpenProcess(cp.PROCESS_ALL_NEEDED, False, pid)
    if not h or not wait_unpacked(h):
        print(f"[{ts()}] pid {pid}: could not attach", flush=True)
        return
    ring = install(h)
    mult = struct.unpack("<I", cp.read(h, 0x7FFE0004, 4))[0]
    seen = 0
    while cp.alive(h):
        hdr = cp.read(h, ring, 4)
        if not hdr:
            break
        idx = struct.unpack("<I", hdr)[0]
        if idx - seen > RING_ENTRIES:
            print(f"[{ts()}] ring overrun, {idx - seen - RING_ENTRIES} records lost", flush=True)
            seen = idx - RING_ENTRIES
        while seen < idx:
            e = cp.read(h, ring + HEADER + (seen % RING_ENTRIES) * ENTRY, ENTRY)
            seen += 1
            tag, tick, ecx = struct.unpack_from("<III", e, 0)
            stack = struct.unpack_from("<10I", e, 12)
            extra = struct.unpack_from("<I", e, 0x34)[0]
            chain = [x for x in struct.unpack_from("<6I", e, 0x38) if x]
            ms = (tick * mult) >> 24
            name = TAGS.get(tag, tag)
            if name == "APPLY" and ecx == 0:
                continue
            desc = {
                "SYNC_RX": lambda: f"entity={extra}",
                "APPLY": lambda: f"entity_obj={ecx:#x} stream bits={extra >> 16} readpos={extra & 0xffff}",
                "READ17": lambda: f"bindings={ecx:#x} +0x61 before={extra:#04x} (islocal={extra >> 3 & 1})",
                "SET17": lambda: f"bindings={ecx:#x} new={extra >> 8} old+0x61={extra & 0xff:#04x}",
                "DTOR": lambda: f"component={ecx:#x}",
                "LOOP_BIT": lambda: f"index={extra & 0xffff} mask0_lo16={extra >> 16:#06x} ret_frame={stack[0]:#x}",
                "ADD_LEN": lambda: f"length={extra & 0xffff} rpc_readpos={extra >> 16}",
                "H_ADD": lambda: f"arg5={stack[5]:#x} [arg5]={extra} args={[hex(x) for x in stack[1:6]]}",
                "H_CREATE": lambda: f"arg5={stack[5]:#x} [arg5]={extra} args={[hex(x) for x in stack[1:6]]}",
                "H_DC": lambda: f"arg5={stack[5]:#x} [arg5]={extra} args={[hex(x) for x in stack[1:6]]}",
                "ADD_COPY": lambda: f"rpc_bits={extra >> 16} copied_bits={extra & 0xffff}",
                "COMP_FOUND": lambda: f"component={extra:#x}",
                "TOD_READ": lambda: f"bindings={ecx:#x} binding={stack[1]} bitpos={extra}",
            }.get(name, lambda: "")()
            print(f"[{ts()}] t={ms} {name:7} {desc} ret={stack[0]:#x} "
                  f"chain=[{' '.join(f'{x:#x}' for x in chain)}]", flush=True)
        time.sleep(0.02)
    print(f"[{ts()}] pid {pid} exited", flush=True)


if __name__ == "__main__":
    main()
