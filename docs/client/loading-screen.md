# Loading screen / "white screen" (client 36731)

All addresses are `SkySagaU.exe` 36731 (image base 0x400000, Ghidra program `/36731`).
Status markers: **[code]** read out of the disassembly, **[run]** observed on a live client,
**[guess]** not yet confirmed.

## Who draws the white screen

`ui_loading_screen`, tick = `FUN_008F6EE0` (vtable `0x012E8BFC`, slot 5). **[code]**

```
tick(this):
    if this[0x194] == 0:
        switch this[0x17C]:          // loading-screen state machine
            case 0: photo slots ...  // ownerName / displayName text boxes
            case 1: ...
            case 3: tips (/_suggest), waits FUN_007abbe0()
            case 4: if !this[0x160] && ModeGlobal[0x1044] && FUN_007abbe0():
                        start background/teleport visuals; this[0x160] = 1
    else if MapSpecValid(ModeGlobal + 0xFC4):        // FUN_00C0CD80
        FUN_008F8780(ModeGlobal + 0xFC4)             // fills map-related list at +0x1B0..+0x1C0
        if list non-empty: this[0x17C] = 4
        this[0x194] = 0
```

`+0x194` is a request flag. While it is set the state machine does not run, and the panel stays
on screen over the world and over fullscreen video. That is the white screen.

`MapSpecValid` (`FUN_00C0CD80`): `spec[0x2E] != 0 && spec[0x30] != 0 && spec[0x0C] != 3`. **[code]**
In practice these are `Searchable[0]`, `Searchable[2]` and a type byte. A default-constructed spec
(`FUN_00C0CC30`, 0x60 bytes) fails the check.

## ModeGlobal + 0xFC4 (current MapSpec)

ModeGlobal is a singleton at `DAT_0143785C`, ctor `FUN_00713050`, dtor `FUN_00713F70`.

Every instruction touching `+0xFC4`, classified **[code]**:

| Address | Function | Role |
|---|---|---|
| `0x00713124` | `FUN_00713050` ctor | default-construct |
| `0x0071473A` | `FUN_00713F70` dtor | destroy |
| `0x00715E2E` | `FUN_00715E10` | **reset** to an empty spec, then game state -> 0xC / 0xB (callback keyed by `DAT_0144A058`, registered in ctor; also referenced by `FUN_008BD3C0`) |
| `0x0080FB21` | `FUN_0080F9E0` | read (GameState_UnloadWorld enter) |
| `0x008F6F2B` | `FUN_008F6EE0` | read (loading-screen tick, above) |
| `0x00964270` | `FUN_00964010` | read (gameplay tick; copies spec OUT into a temp) |
| `0x00964BB0` | `FUN_00964BA0` | **the only writer**: TeleportBegins handler |
| `0x00BAC266` | `FUN_00BAC260` | getter `return p + 0xFC4` |

`FUN_007176F0` is `MapSpec::operator=` (`this` = destination, arg = source).

## TeleportBegins (RPC ordinal 35 / 0x23)

- Registered by `FUN_00964C00(FUN_00964BA0, state)`. The caller is `FUN_009644F0`, the gameplay
  state's Enter, which also clears flag bits 1 and 2 and sets bit 0 (`flags = flags & 0xF9 | 1`).
  The handler does not exist before gameplay is entered, so a 35 sent earlier gets dropped. **[code]**
- Receive stub `FUN_00964EE0` reads exactly one MapSpec (`FUN_007E9810`) and calls the handler. **[code]**
- Handler `FUN_00964BA0`: `state.flags(+0x38) |= 2; ModeGlobal.FC4 = spec`. **[code]**

## Gameplay tick after TeleportBegins (`FUN_00964010`)

`state.flags` at `+0x38`: bit0 = entered, bit1 = teleport pending, bit2 = ready. **[code]**

```
if !(flags & 2):
    if DAT_01440E98[0x70]: "Level unload"; flags |= 5
else:
    "Level unload"; flags.bit2 = <world/scene ready checks: FUN_007D08A0, FUN_00B43D70, FUN_006EF5F0,
                                  FUN_00785770(0x42), FUN_008DC8D0, FUN_00965280, FUN_007ABBE0>
if (flags & 5) == 5:
    FUN_00C62DF0()
    if !DAT_01440E98[0x70] && FUN_006EF5F0() && !slot[0x45]:
        slot[0x45] = copy of ModeGlobal.FC4      // FUN_00777A40 -> FUN_00785820(.., 0x7D)
    ModeGlobal.state.Change(0xC)                 // FUN_00718D00, states 0..0xE at ModeGlobal+0xCE0
```

`FUN_00777A00` / `FUN_00777A40` test and set slot N in a 125-slot (0x7D) table.
They are not network calls. **[code]**

## Server-side consequence

- TeleportBegins is the only way to make `+0xFC4` valid and unblock the loading screen.
- After it, the client reconnects. **[run]** The reconnect path itself is not in this doc yet:
  find out what triggers the disconnect and what the client expects on the second connection.
  On the second connection the white screen still stays up. **[run]**
- `FUN_00715E10` resets `+0xFC4` to an empty spec. If it fires during the reconnect, the spec
  from the first connection is lost. **[guess]** Breakpoint on `0x00715E2E` to check.
