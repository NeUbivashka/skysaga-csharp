# Client-to-server RPC payloads (36731)

Payloads of the RPCs the client sends, read from their send stubs. Each stub calls
`FUN_00BC56B0(ordinal, ...)` to start the packet, writes the fields inline or through a writer, and
then optionally logs the call as JSON. Most JSON printers are in the packed part of the binary, so
field names are only known where a printer could be read. The stub addresses are in
`docs/reference/data/rpc_client.json`.

Conventions: "ranged max M" is `32 - clz32(M)` bits. "Full-width u32" is 32 bits in network
(big-endian) byte order. Server handlers live in `Servers/SkySaga.Game/Packets`.

| Ordinal | RPC | Stub | Payload |
|---:|---|---|---|
| 18 | UISettingsSetHotbarSlotItemSpec | `FUN_009C3BE0` | slot ranged max 5 (3 bits), hand ranged max 2 (2 bits, 0 = left, 1 = right), item reference (writer `FUN_0085A490`) |
| 19 | UISettingsSelectHotbarSlot | `FUN_009C30B0` | slot ranged max 5 (3 bits) |
| 17 | UISettingsSetGameplayCameraState | `FUN_009C37D0` | third-person camera 1 bit, state ranged max 1 (1 bit, unknown) |
| 20 | UISettingsSetMiniMapSettings | `FUN_009C4080` | 2 bools, then two lists (`FUN_009C4C90`: count escaped at 0x19; `FUN_009C2F80`). Not handled yet |
| 21 | UISettingsSetUISettings | `FUN_009C4420` | see below, 8 bits |
| 22 | UISettingsSetControlSettings | `FUN_009C3460` | 1 bit (meaning unknown) |
| 51 | SetPlayerState | `FUN_00B25D10` | state ranged max 10 (4 bits) |
| 76 | SetInputMovement | `FUN_00B24140` | direction 3 × ranged 0x80 of (v + 0x40), speed ranged 0x78 (see entity-sync.md) |
| 135 | JoinDestinationSelectionWorld | `FUN_007B85E0` | triggeringEntityID: full-width u32 |
| 155 | EntityMoved | `FUN_00B2A460` | X 17, Y 15 (+0x3FC0), Z 17, yaw 15 bits (see entity-sync.md) |
| 160 | SetLookAtDirection | `FUN_0086D540` | pitch, yaw: each ranged max 0x6400 of (v + 0x3200), 15 bits, 1/32° (`FUN_00859000`) |
| 170 | TouchedVoxelTrackingSyncUp | `FUN_00B2D5C0` | count escaped at 100 (7 bits), then per entry a full-width u32 and 8 bits (`FUN_00B2DB60`). Not handled yet |
| 280 | PlayerDodged | `FUN_00B25580` | ranged max 8 (4 bits) |
| 295 | PerformJump | `FUN_00B23E30` | nothing |

## UISettingsSetUISettings (21)

Writer `FUN_009C4DE0`, JSON printer `FUN_009C4AF0` (field names below):

| field | wire |
|---|---|
| DisableAutoQuestTracking | 1 bit |
| DisableSocialNotifications | 1 bit |
| DisableMysteryBoxNotifications | 1 bit |
| DisplayTimeMode | ranged max 2 (2 bits) |
| QuestTrackerSort | ranged max 4 (3 bits) |

Observed bytes `00` and `08`. `08` is DisplayTimeMode = 1.

## Settings come back through sync

The RPCUISettings* calls only report a change. The client applies settings from the Player's
`clientsettingscomponent` (bindings vtable `0x013456C0`, read `FUN_00C265A0`), so the server stores
them there and the binding syncs them back. They are also saved in `player.json` under `Settings`.

| binding | sync | parameter | wire |
|---|---|---|---|
| 0 | 74 | hotbarslotresources | count escaped at 6 (3 bits), then per slot a full-width u32 (the server sends the slot number) and two item references, left hand then right hand (`FUN_00C26E70` / `FUN_00C26D30`). Verified: items show on the hotbar |
| 1 | 94 | lastactivatedslot | ranged max 5 (3 bits, `FUN_00C25D30`) |
| 2 | 33 | currentgameplaycamerastate | third-person camera 1 bit (`+0xBD`; 0 = first person, the client default, so the player never sees their own character), then state ranged max 1 (`+0xBC`, `FUN_00C25C50`) |
| 3 | 116 | minimapsettings | not sent yet |
| 4 | 161 | uisettings | the same 8 bits as RPC 21 (`FUN_00C27030`) |
| 5 | 27 | controlsettings | 1 bit (`+0xBE`) |

## SetPlayerState (51)

The value is the local character state byte (character state object `+0x28`), sent from
`FUN_00B24BB0` whenever it changes. Values seen: 0, 2, 3, 4, 5. When the state is 7, the same function
also sends PlayerDodged. If entity table entry 0x5B exists, it also sends the RPC from `FUN_00B251C0`.
The state names have not been found. The Player's own `characterstateid` (sync 22) is the
server-side counterpart, not yet synced.

## Equipped items

`clientequippeditemcomponent` (bindings vtable `0x01341B1C`, read `FUN_00BFF590`):
`equippeditemspecs` (sync 50) is a count escaped at 9 (4 bits), then one item reference
(`FUN_007E9440`) per equipment slot. The slots are LeftHand, RightHand, Head, Torso, Legs, Arms,
Device, Backpack, AdditionalHeadFeature (names at `0x013415D4`). `autoequip` (sync 9) is 1 bit, and
`legactions` (sync 102) is a presence bit + u32. With armour equipped and the camera in third person,
the customised character shows in the world (verified 2026-10-12).
