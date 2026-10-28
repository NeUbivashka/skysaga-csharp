import argparse
import json
import os
import re
import struct
import urllib.parse
import urllib.request

BASE = os.environ.get("GHIDRA_URL", "http://127.0.0.1:8089")
PROGRAM = os.environ.get("GHIDRA_PROGRAM", "/36731")
RPC_SEND = 0x00BC56B0


def get(endpoint, **params):
    params["program"] = PROGRAM
    url = f"{BASE}/{endpoint}?{urllib.parse.urlencode(params)}"
    with urllib.request.urlopen(url, timeout=300) as response:
        return json.load(response)


def read(address, length):
    return bytes(b & 0xFF for b in get("read_memory", address=f"{address:08x}", length=length)["data"])


def crc(name):
    value = 0
    for byte in name.lower().encode("latin-1"):
        value ^= byte << 24
        for _ in range(8):
            value = ((value << 1) ^ 0x04C11DB7) & 0xFFFFFFFF if value & 0x80000000 else (value << 1) & 0xFFFFFFFF
    return value


def decompile(args):
    for address in args.addresses:
        result = get("decompile_function", address=address)
        print(f"// ===== {result.get('name')} @ {address}")
        print(result.get("decompiled", result))
        print()


def stub(args):
    pattern = f"6a{args.ordinal:02x}" if args.ordinal < 0x80 else "68" + struct.pack("<I", args.ordinal).hex()
    for hit in get("search_byte_patterns", pattern=pattern, limit=5000):
        if "address" not in hit:
            continue
        start = int(hit["address"], 16)
        code = read(start, 48)
        for i in range(2, 43):
            if code[i] == 0xE8 and (start + i + 5 + struct.unpack("<i", code[i + 1:i + 5])[0]) & 0xFFFFFFFF == RPC_SEND:
                owner = get("get_function_by_address", address=f"{start:08x}").get("name")
                print(f"push at {start:08x}, call at {start + i:08x}, in {owner}")


def vtable(args):
    for address in args.addresses:
        values = struct.unpack("<8I", read(int(address, 16), 32))
        print(address, " ".join(f"{v:08x}" for v in values))


def string(args):
    for address in args.addresses:
        print(address, read(int(address, 16), 128).split(b"\0")[0].decode("latin-1"))


def strings(args):
    for entry in get("list_strings", filter=args.text, limit=args.limit)["strings"]:
        print(entry["address"], repr(entry["value"]))


def xrefs(args):
    for address in args.addresses:
        for reference in get("get_xrefs_to", address=address, limit=200)["references"]:
            print(address, "<-", reference["from_address"], reference["type"], reference.get("from_function"))


def crc_table(args):
    count = args.count
    hashes = struct.unpack(f"<{count}I", read(int(args.address, 16), count * 4))
    candidates = set()
    for path in args.sources:
        text = open(path, "rb").read()
        for match in re.finditer(rb"[A-Za-z_][A-Za-z0-9_ ]{1,60}", text):
            word = match.group().decode("latin-1")
            candidates.add(word)
            candidates.update(part for part in re.split(r"[ _]", word) if part)
    lookup = {}
    for word in candidates:
        lookup.setdefault(crc(word), word)
    for index, value in enumerate(hashes):
        print(f"{index:3d} 0x{index:02X} {value:08x} {lookup.get(value, '?')}")


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(required=True)

    command = commands.add_parser("decompile")
    command.add_argument("addresses", nargs="+")
    command.set_defaults(run=decompile)

    command = commands.add_parser("stub")
    command.add_argument("ordinal", type=int)
    command.set_defaults(run=stub)

    command = commands.add_parser("vtable")
    command.add_argument("addresses", nargs="+")
    command.set_defaults(run=vtable)

    command = commands.add_parser("string")
    command.add_argument("addresses", nargs="+")
    command.set_defaults(run=string)

    command = commands.add_parser("strings")
    command.add_argument("text")
    command.add_argument("--limit", type=int, default=50)
    command.set_defaults(run=strings)

    command = commands.add_parser("xrefs")
    command.add_argument("addresses", nargs="+")
    command.set_defaults(run=xrefs)

    command = commands.add_parser("crc")
    command.add_argument("names", nargs="+")
    command.set_defaults(run=lambda a: [print(f"{crc(n):08x} {n}") for n in a.names])

    command = commands.add_parser("crc-table")
    command.add_argument("address")
    command.add_argument("count", type=int)
    command.add_argument("sources", nargs="+")
    command.set_defaults(run=crc_table)

    args = parser.parse_args()
    args.run(args)


if __name__ == "__main__":
    main()
