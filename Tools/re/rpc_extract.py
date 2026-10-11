"""
Extract the client's RPC table and per-RPC stub functions from the Ghidra project (HTTP API on
127.0.0.1:8089, program SkySagaU.exe = client 36731). Output: docs/reference/data/rpc_client.json.

What it collects, per ordinal:
  name        from the RPC name table at 0x01409524 (entry 0 is "RPC-CALL"; ordinal = index - 1)
  send        functions that call the RPC stats hook FUN_00BB3E50(0, ordinal, bits, json) - the
              client's sender for a client->server RPC
  receive     functions that call FUN_00BB3E50(1, ordinal, ...) - the receive stub that unpacks a
              server->client RPC and dispatches the handler

Field names are NOT recoverable statically: the JSON the client logs is built in virtualized code
and the names only exist at run time. Tools/re/rpc_observe.py harvests them from engine.log.

    python rpc_extract.py
"""

import json
import re
import struct
import sys
import urllib.parse
import urllib.request
from pathlib import Path

API = "http://127.0.0.1:8089"
PROGRAM = "SkySagaU.exe"
NAME_TABLE = 0x01409524
NAME_COUNT = 342
STATS_HOOK = 0x00BB3E50
OUT = Path(__file__).resolve().parents[2] / "docs" / "reference" / "data" / "rpc_client.json"


def get(path, **params):
    params["program"] = PROGRAM
    url = f"{API}/{path}?{urllib.parse.urlencode(params)}"
    return json.load(urllib.request.urlopen(url, timeout=600))


def post(path, body):
    req = urllib.request.Request(f"{API}/{path}?program={PROGRAM}", data=json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req, timeout=600))


def read(addr, n):
    return bytes.fromhex(get("read_memory", address=hex(addr), length=n)["hex"])


def cstring(addr):
    data = read(addr, 128)
    return data.split(b"\0", 1)[0].decode("ascii", "replace")


def disasm(start, end):
    return post("disassemble_bytes", {"start_address": hex(start), "end_address": hex(end)}).get("instructions", [])


def imm(op):
    m = re.fullmatch(r"(-?0x[0-9a-fA-F]+|-?\d+)", op.strip())
    return int(m.group(1), 0) if m else None


def pushes_before(call_addr, window=48):
    """Immediate pushes directly before a call, nearest first (= first argument first)."""
    ins = [i for i in disasm(call_addr - window, call_addr - 1)]
    out = []
    for i in reversed(ins):
        mn = i.get("mnemonic", "").upper()
        if mn == "PUSH":
            out.append(imm(i.get("operands", "")))
        elif mn == "CALL":
            break
    return out


def function_of(addr):
    try:
        f = get("get_function_by_address", address=hex(addr))
        return int(f["address"], 16), f["name"]
    except Exception:
        return None, None




def main():
    names = {}
    table = read(NAME_TABLE, NAME_COUNT * 4)
    for i in range(1, NAME_COUNT):
        ptr = struct.unpack_from("<I", table, i * 4)[0]
        names[i - 1] = cstring(ptr) if ptr else None

    rpcs = {o: {"ordinal": o, "name": n, "send": [], "receive": []}
            for o, n in names.items()}

    refs = get("get_xrefs_to", address=hex(STATS_HOOK), limit=5000)["references"]
    unresolved = []
    for r in refs:
        site = int(r["from_address"], 16)
        args = pushes_before(site)
        if len(args) < 2 or args[0] not in (0, 1) or args[1] is None or args[1] not in rpcs:
            unresolved.append(r["from_address"])
            continue
        direction, ordinal = args[0], args[1]
        faddr, fname = function_of(site)
        entry = {"function": fname, "address": f"0x{faddr:08X}" if faddr else None, "call": f"0x{site:08X}"}
        rpcs[ordinal]["send" if direction == 0 else "receive"].append(entry)
        print(f"{ordinal:3} {names.get(ordinal)!s:45} {'send' if direction == 0 else 'recv'} {fname}", flush=True)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"source": "SkySagaU.exe 36731 via Ghidra",
                               "unresolved_stats_calls": unresolved,
                               "rpcs": list(rpcs.values())}, indent=1), encoding="utf-8")
    print(f"wrote {OUT} ({len(refs)} stats calls, {len(unresolved)} unresolved)")


if __name__ == "__main__":
    main()
