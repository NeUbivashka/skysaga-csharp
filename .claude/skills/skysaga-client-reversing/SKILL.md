---
name: skysaga-client-reversing
description: Reverse-engineer how the SkySaga 36731 client (Alpha 10) sends, reads and uses an RPC, a component field, an HTTP endpoint or a GeoData-driven rule, using a read-only Ghidra HTTP bridge. Use before implementing or changing any packet, sync field or web API in Servers/Alpha10, and whenever a value, layout or meaning must be confirmed against the client.
---

# Reversing the SkySaga 36731 client

The emulator is only correct if every bit on the wire and every rule matches what the 36731 client
does. This skill is the workflow for finding that out. Write the result to the wiki with the
`skysaga-wiki-writing` skill.

## Ground rules

- **Source of truth.** The 36731 client binary and `Bundled/36731` (GeoData.json, Entities.json)
  are the only ground truth.
  - Code inherited from EDITzDev targets Alpha 3. It may look right and still be wrong for 36731.
  - The same applies to old wiki text and to other contributors' PRs. Re-check them.
- **No invented constants.** Every number on the wire or in a rule must come from the client or
  GeoData. If a value cannot be found, say so and mark the server's choice as an emulator choice.
- **Ghidra is read-only.** Never rename, retype or comment anything in the shared Ghidra project.
  Findings go to the wiki.
- **No debugger on the live client.** Attaching x32dbg crashed the 36731 client twice. Use
  static analysis. If a debugger is unavoidable, ask the player first, since the game will close.
- **Record negative results.** A sender with no caller (dead code) or an RPC with no handler is a
  result too. Write it down so nobody reverses it again.

## Tools

Ghidra runs with an HTTP bridge on `http://127.0.0.1:8089`, program `/36731`. Override both with
the `GHIDRA_URL` and `GHIDRA_PROGRAM` environment variables. Endpoints used:

| Endpoint | Parameters |
|---|---|
| `decompile_function` | `address` |
| `disassemble_function` | `address` |
| `get_function_by_address` | `address` |
| `get_xrefs_to` | `address`, `limit` |
| `list_strings` | `filter`, `limit` (substring, case-insensitive) |
| `read_memory` | `address`, `length` |
| `search_byte_patterns` | `pattern` (hex), `limit` |

`scripts/ghidra_tools.py` wraps them. Run it with plain `python`, not `python -I`, because it only
imports the standard library:

```bash
python scripts/ghidra_tools.py decompile 007d2b00 008012d0   # decompile one or more functions
python scripts/ghidra_tools.py stub 90                       # find the send stub of RPC ordinal 90
python scripts/ghidra_tools.py vtable 012c0630               # 8 dwords at a vtable
python scripts/ghidra_tools.py string 012ee508               # C string at an address
python scripts/ghidra_tools.py strings buildpermission       # search strings
python scripts/ghidra_tools.py xrefs 00b428e0                # who references an address
python scripts/ghidra_tools.py crc Hit Landed                # SkySaga name CRC
python scripts/ghidra_tools.py crc-table 013840f8 208 <files> # name a CRC table by brute force
```

Decompiler output drops arguments of `thiscall` calls and register-passed values. When the
argument order matters, read `disassemble_function` (count the `PUSH`es before the `CALL`; `RET n`
gives the stack size).

## Landmarks in 36731

| Address | What it is |
|---|---|
| `0x01409524` | RPC name table. Entry 0 is `RPC-CALL`, so ordinal = index − 1 |
| `0x00BC56B0` | Starts an outgoing RPC. Each send stub does `PUSH ordinal` and then `CALL 0x00BC56B0` |
| `FUN_007CF5A0` | `ClientNet_RegisterRpcHandlers`: one register function per RPC the client receives |
| `[0x01487BCC]` | GeoData root. Tables hang off it at fixed offsets (wiki Entity-Sync, "Client table sizes") |
| `FUN_00716DA0` | HTTP response envelope: uses `result` if present, otherwise the root; errors from `error` |
| `FUN_00B428E0` | `Entity_FireEvent(event, instigator)`. Event names are a CRC table at `0x013840F8` (208 entries) |
| `0x01404C90` | Game mode names (0 HomeIsland, 1 Adventure, 4 Social, …), sent in MapDefinition |

## Finding a client-to-server RPC

1. Get the ordinal and wire id from wiki RPC-Ordinals.
2. Run `stub <ordinal>`.
   - Ordinals below 0x80 are pushed as `6A xx`; larger ones as `68 xx xx xx xx`.
   - No hit means the client never sends it. Check that the name has no other code reference.
3. Disassemble the stub. After the `CALL 0x00BC56B0` the stub writes each field into the
   bitstream returned in `EAX`; then it builds a debug JSON log line, which you can ignore.
4. Decompile each field writer and map it to a primitive (table below).
5. `xrefs` on the stub gives its caller. The caller tells you when it is sent and what each
   argument means: UI callback, input action, component update.
   - A caller with no references of its own may be a virtual method.
   - Search for its address as a little-endian byte pattern to find the vtable or callback table.
   - Nothing at all means dead code.

## Finding a server-to-client RPC

1. Wiki RPC-Ordinals, section "Client handlers", lists the handler and the register function per
   ordinal. Missing ordinals have no handler: the client ignores them.
2. Decompile the register function. It allocates a reader object (`*obj = &PTR_FUN_xxxx`) and
   stores it under the ordinal.
3. Run `vtable` on that vtable. Entry 0 is the destructor and **entry 1 is the reader**.
4. Decompile the reader. It reads the fields in order, logs them, and calls the handler through
   its stored object (vfunc +4) with pointers to the read values.
5. Decompile the handler to learn what the client does with each field. That is what the server
   must get right.

## Bitstream primitives

All values are written MSB-first into the bitstream.

- **Byte order.** Multi-byte values are byte-swapped when the stream is in network order. That
  is the check `FUN_006FB780` / `FUN_007187F0` followed by `FUN_00EA7700` / `FUN_00EA7730`. The
  server's `BitStream` helpers already do this.

| Client code | Meaning | Bits |
|---|---|---|
| `NumLeadingZeros32(max)` then `ReadBits(32 - clz)` | ranged value 0..max | bits needed for `max` |
| `ReadBits(0x20)` | full-width u32 | 32 |
| `ReadBits(0x40)` | u64 (times are milliseconds since 1970) | 64 |
| `FUN_00EA6FA0` / `FUN_00EA6F40` (0) / `FUN_00EA6F60` (1) | one bit | 1 |
| `FUN_007EA3B0` read, `FUN_007B0D70` write | optional UUID: presence bit, then four full-width u32 | 1 or 129 |
| `FUN_007EA4B0` | optional CRC: presence bit, then a full-width u32 | 1 or 33 |
| `FUN_007E7D10` | optional entity id: presence bit, then a full-width u32 | 1 or 33 |
| `FUN_007EA530` | string | length-prefixed |
| `FUN_00857520` | compact count: bit 0 → 7-bit value ≤ 0x40, bit 1 → full u32 | 8 or 33 |
| `FUN_007B8200` | ranged index into a GeoData table: max = table count − 1 | bits for count − 1 |

Escaped counts (a list length with an escape value) and the UUID word order are described in
wiki Packet-Format. On the server use `ReadClientUuid` / `WriteClientUuid`, `WriteRanged`,
`WriteUInt64`, `WriteEscapedCount` and `InventoryItemRef.WriteOptionalUInt32`.

## HTTP endpoints

The client builds a URL with a format string. Search for it with `strings /api/`. Each request
has three parts:

- **Builder.** It writes `params` (optional wrapper) and the body keys with `FUN_005E45D0(key)`.
- **Method.** `FUN_00D1B9D0` sends POST and `FUN_0095C420(..., 3)` sends GET. PUT shows up only
  in the proxy log.
- **Response handler.** It checks success (vfunc +4), then walks the JSON. `FUN_00C0AE60(key)`
  matches a child key. `node + 0x18` is the node type: 1 object, 2 array, 3 string, 4 integer,
  6 boolean. `node + 0x10` is the value and `node + 8` the first child.

A count read as type 4 must be a JSON integer; a float or string counts as missing. Many action
handlers ignore the body and only need `200` with JSON. Say so in the wiki when that is the case.

## Names from CRCs

GeoData and client tables refer to names by CRC32. The polynomial is 0x04C11DB7, MSB-first, on
the lower-cased name, with initial value 0 (`Util.ComputeCrc32`).

- **Recovering names.** To name a CRC table, hash every string of the client binaries (all builds
  under `D:\codya\SkySaga\*\Client\SkySaga.exe`) and of GeoData/Entities, then match (`crc-table`).
- **Partial results.** Unmatched entries stay unknown. Do not guess names.

## Enum and table names

Lists of names that sit next to each other in `.rdata` often are the enum, for example the game
mode list at `0x01404C90`. To find one:

1. Search one known value with `strings`.
2. Read memory around it.
3. Look for an array of string pointers. The entry index is the enum value, and the last entry
   is often the count (`Noof`).

## Where the client fires things itself

Before making the server send an event or a state change, check whether the client already does
it locally. Otherwise it happens twice.

- **Example.** `Entity_FireEvent` has 110 call sites with constant event ids (Jump, Dead, Spawn,
  Landed…). Attack and Hit never appear there, so only the server can trigger them.
- **Method.** Collect the constant arguments at each call site from `disassemble_function`.

## Checklist before writing it up

- [ ] Every field: bit width, encoding, byte order, range, meaning and source function.
- [ ] Who sends it and when (caller chain), or proof that nothing does.
- [ ] What the receiving side does with each field (handler behaviour, side effects, UI).
- [ ] Every constant traced to the client or GeoData; the rest marked as an emulator choice.
- [ ] Known unknowns listed explicitly.
