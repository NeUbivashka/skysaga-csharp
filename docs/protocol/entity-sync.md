# Entity sync format (36731)

How entity state travels from server to client: the `EntityAdd` / `EntitySync` packets, the
per-entity sync block inside them, and the parameter encodings of the world-level entities
(TimeOfDay, WorldComposition). All addresses are `SkySagaU.exe` 36731 (no ASLR).

## BitStream conventions

RakNet `BitStream`: `+0` bits used, `+8` read offset, `+0xC` data pointer.

- `ReadBits(dst, n, alignRight=true)` fills `dst` little-endian per byte. A field of `n` bits
  written by the server as `WriteBits(BitConverter.GetBytes(v), n, true)` reads back as `v`.
- Ranged ints are written in `32 - clz32(max)` bits (`Util.NumBitsRequiredUInt32` returns the
  leading-zero count, despite its name).
- Ranged *indices* into a client table of `count` entries use `16 - clz16(count - 1)` bits
  (`WriteRangedIndex`). Index 0 means "unset"; real entries start at 1.
- `BitStream.Write(BitStream)` consumes the source's read pointer. Serialising the same
  `SyncData` twice without `ResetReadPointer()` sends an empty block the second time (this is
  why only 13-byte EntityAdds reached the client before the fix).

## Escaped length (FUN_007D7490)

Used both for the outer length in EntityAdd/EntitySync and for the inner parameter-block length:

| bits | meaning |
|------|---------|
| 1 | escape: 0 = short form, 1 = long form |
| 18 | length in bits (short form, `<= 0x20000`) |
| 32 | length in bits (long form) |

## EntityAdd (client stub FUN_007F1140)

| field | encoding |
|-------|----------|
| type hash | optional (presence bit + 32 bits) |
| entity id | int |
| parent id | optional |
| sync block length | escaped length |
| sync block | see below |

The stub copies the sync block into its own stream and wraps it (FUN_00C71550). The RPC
dispatcher (FUN_00DBAE80 / FUN_00DBAE00) builds the packet stream from `packet+0x30`, length at
`packet+0x28`.

## Sync block (Entity_ApplySyncEx FUN_00B43760)

1. **Dirty mask**: one bit per *synced* parameter (`SyncedParametersCount`, e.g. Player = 169).
   - Full 32-bit words: written byte-reversed (big-endian). The client reads each word and swaps
     it (FUN_00B44AD0).
   - Tail (`count % 32` bits): natural order, right-aligned. It is swapped only on a big-endian
     host (FUN_00EA76A0), so never on x86. Reversing the tail drops parameters 160..168 of Player.
2. **Parameter block length**: escaped length (above). Leaving out the escape bit shifts every
   parameter by one bit. This was the cause of the "flaky islocal" bug.
3. **Parameters**: for each set mask bit, in sync-index order, the parameter's own encoding.

### Client side lookup

- The entity type's param table sits at `type+0x44..0x48`, 0x1C bytes per entry: `+8` component
  hash, `+0x10` binding index, `+0x18` sync index. `type+0x86` = param count.
- Components sit at `entity+0x90`. Each component's bindings object is at `component+0x20`, and
  vtable slot 2 is `read(binding, stream)`.
- Sync index order is the alphabetical order of parameter names in `Data/Entities.json`.
  Binding order is the component's own order. They differ, so always map through the names.

## World entities

### TimeOfDay — clienttimeofdaycomponent

Read FUN_00D462E0, bindings vtable 0x0135DCC8.

| sync | binding | parameter | wire | stored |
|------|---------|-----------|------|--------|
| 0 | 2 | daynightcycleduration | ranged, max 0x780 | ×1/64 → `+0x14` |
| 1 | 1 | fixedtimeofday | bool | `+0x20` bit 0 |
| 2 | 3 | realworldstarttime | 64-bit | `+0x08` |
| 3 | 0 | starttimeofday | ranged, max 0x10000 | `+0x10` |
| 4 | 5 | timeofdayoffset | ranged, max 0x10000 | `+0x1C` |
| 5 | 4 | timestretch | ranged, max 0x1FC0 | ×1/64 → `+0x18` |

`Timer_GetFraction` (FUN_00D45E60) divides by daynightcycleduration, so a zero duration crashes
the client. Known issue: the server writes the two halves of realworldstarttime in the wrong order.

### WorldComposition

Client components: `clientadventurequestscomponent` (primaryquestdata, secondaryquestlist),
`clientworldcompositioncomponent` (palettecrc, terraintype, minwalkableyposition),
`eventmanagercomponent` (activeeventlist, eventcalendar).

Sync indices (Entities.json): activeeventlist 0, eventcalendar 1, minwalkableyposition 2,
palettecrc 3, primaryquestdata 4, secondaryquestlist 5, terraintype 6.

**clientworldcompositioncomponent**: factory FUN_00B07110 → ctor FUN_00B35020 (vtable
0x01333AA4) → base FUN_00C7A0F0 (vtable 0x013498F8, bindings sub-object at `+0x38`, vtable
0x013498D8). Binding names → indices: FUN_00B15040. Read function: FUN_00C7A7E0.

| binding | parameter | reader | wire | stored |
|---------|-----------|--------|------|--------|
| 0 | palettecrc | FUN_007D7C90 | ranged index into the BiomePalette table, 6 bits for 46 entries | `+4` (u16) |
| 1 | terraintype | FUN_00BD82A0 | 3 bits (max 6) | `+6` (u8) |
| 2 | minwalkableyposition | FUN_00C7A610 | 8 bits = value + 100, clamped to -100..100 | `+8` (i32) |

Each change sets a dirty bit at `this-0x30` (1, 2, 4) and notifies the owner through `this-0x38`
vtable `+0xC`.

**palettecrc is an index, not a CRC.** The table is the client's loaded BiomePalette list:

- `[0x01487BCC] + 0x19DC`: pointer vector. Entry 0 is null ("unset"), entries 1..45 are palette
  objects (0x380 bytes each, CRC of the name at `+0xC`).
- `+0x19E0`: count = 46.
- After the pointers: the CRCs sorted, a u16 permutation (sorted → index), and the CRCs in index
  order. This is how the client resolves a name CRC to an index.
- The name CRC is the engine's lower-case CRC32 (`bhash`).

Indices measured in a live client:

| index | palette | CRC |
|-------|---------|-----|
| 29 | BiomePalette_FTUE_The_Old_City | 0xBC9FADBF |
| 43 | BiomePalette_SocialHub | 0x00B42798 |

These match MapSpec slot 4 (also 1-based, count 46).

**TerrainType enum** (name table at 0x01404A0C, followed by `Noof`):

| value | name |
|-------|------|
| 0 | Sky |
| 1 | Sky_BonusWorld |
| 2 | Surface |
| 3 | Peak |
| 4 | Subterranean |
| 5 | Enclosed |

GeoData uses these names in `DefaultTerrainType` / `Terrain`. FTUE_The_Old_City is `Sky`.

### SeaLevel (not yet implemented server-side)

Components: `clientnodecomponent` (nodecrc, disableworldnode, inifiniteupdatedistance,
deletenodeatdistance) and `sealevelcomponent` (seafloorlevel, sealevel, showsea). Sync: nodecrc 0,
seafloorlevel 1, sealevel 2, showsea 3 (default true). The readers have not been traced yet.

## Player parameters checked against the client readers

One wrong width shifts every later parameter of the entity, and nothing on the client reports
it: values silently become garbage. `SKYSAGA_SYNC_TRACE=1` logs every parameter the server writes
(sync index, component, name, width) so each one can be compared with the reader below.
A component's `TrySync` must return `false` for every parameter it does not write. Returning
`true` sets the mask bit without data.

Fixed 2026-10-12. Together these were why the player could not move and the HUD never unlocked:

| sync | parameter | client reader | wire |
|------|-----------|---------------|------|
| 12, 36 | availableplayersigillist, currentsigil | ClientPlayerNameComponent | were not sent at all (TrySync returned true without data). Now: list = count escaped at 0x100 (9 bits), per entry sigil index (5 bits) + 2 full-width u32 (FUN_00C535D0 / FUN_00C53760); currentsigil = ranged index into the 27-entry sigil table (5 bits, FUN_00C53190) |
| 28 | craftingdropslots | FUN_00D18620 | count escaped at 2 (2 bits), then exactly 2 item refs |
| 78 | inputmovementdirection | FUN_00BF46F0 case 2 / FUN_007D6590 | 3 × ranged 0x80 of (v + 0x40), ×1/64 |
| 79 | inputmovementspeed | case 3 / FUN_00BF4580 | ranged 0x78, ×1/8 |
| 81 | inventoryentitylist | FUN_00BC7550 | count escaped at 0xF9 (8 bits), full-width u32 ids |
| 114 | maxinventoryslots | FUN_00C3F490 | ranged 0xF0 = 8 bits (was 6) |
| 135 | position | FUN_00BF88F0 / FUN_007D66A0 | X 17, Y 15 of (y·64 + 0x3FC0), Z 17 (was 17/17/17) |
| 162 | unlockedfeaturecrcset | FUN_00C426B0 | count escaped at 62, then presence bit + full-width CRC32 of the feature name, **sorted ascending** (the client binary-searches the set; see client/ui-screens.md, HUD visibility) |
| — | scale | FUN_007D6410 | ranged 0x38, ×1/8 (was raw float bits) |

Checked and correct: the aspect bools (CanEditMap 0 … IsSpectator 5, IsDebugPlayer 9, IsLocal 0x11:
1 bit each, FUN_00C75410), takeonly (1 bit), joblist (count escaped at 64), locationspecificquestsready
(1 bit), playername (string), yawdegrees (15 bits), the customisation flag and CRCs.

### Item reference (FUN_007E9440, `InventoryItemRef`)

| field | wire |
|-------|------|
| kind | ranged index into the item table (`[0x01487BCC]+0x1F28`, count `+0x1F2C` = 1810) → 11 bits |
| amounts | count escaped at 4 (3 bits), each presence + u32 |
| quantity | presence + full-width u32 |
| item uuid | presence + 4 × u32 |
| detail | 2-bit kind; 0 → optional uuid, 1 → string, 2 → nothing |

### Movement

The owning client sends **RPCSetInputMovement** (76): direction 3 × 8 bits (v + 0x40) and speed
7 bits. It moves only after the server has applied the input to `clientcharacterphysicscomponent`
and synced it back. **RPCEntityMoved** (155) reports the resulting position: X 17, Y 15 (+0x3FC0),
Z 17, yaw 15 bits, with no entity id. Verified 2026-10-12: holding W moved the character from
z = 3.5 to 26.9 in 3 s.

### Live table sizes (`[0x01487BCC]` + offset, 36731)

| offset | table | count |
|--------|-------|-------|
| 0x19C8 | backstories | 22 |
| 0x19E0 | biome palettes | 46 |
| 0x1DDC | materials | 522 |
| 0x1EC0 | sigils | 27 |
| 0x1F2C | items | 1810 |
| 0x201C | tribes | 36 |
| 0x3E70 | material customisations | 266 |

## UUIDs on the wire

Four 32-bit words, behind a presence bit. The client prints each word low nibble first, so word
`i` = `int(reverse(hex group i of ToString("N")), 16)`, written as big-endian bytes
(`BitStreamExtensions.WriteClientUuid`).
