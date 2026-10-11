# Loading screen / "white screen" (client 36731)

All addresses are `SkySagaU.exe` 36731 (image base 0x400000, Ghidra program `/36731`).
Status markers: **[code]** read out of the disassembly, **[run]** observed on a live client,
**[guess]** not yet confirmed.

## ui_loading_screen gate (first suspect, see below: not the final cause)

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
on screen over the world and over fullscreen video. It looked like the white screen, but see "The white screen was ui_portalfade".

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
    if !DAT_01440E98[0x70] && FUN_006EF5F0() && !IsScreenOpen(0x45):
        OpenScreen(0x45, arg = copy of ModeGlobal.FC4)   // ui_loading_screen, FUN_00777A40
    ModeGlobal.state.Change(0xC)                 // FUN_00718D00, states 0..0xE at ModeGlobal+0xCE0
```

`0x45` is the UI screen id of `ui_loading_screen` (its ctor `FUN_008F6660` registers it).
`FUN_00777A00` / `FUN_00777A40` are the screen manager's IsOpen / Open, see `ui-screens.md`. **[code]**

## The white screen was ui_portalfade, not the loading screen

Live snapshot of the white frame **[run]**: `ui_loading_screen` (0x45) was *closed*, its tick at fsm 4.
The open overlay was `ui_portalfade` (screen 0x42, ctor `FUN_008DC580`), stuck on its `Fade` animation.
The intro video was playing underneath, faintly visible through the white.

### ui_portalfade **[code]**

Animation/event name hashes are in globals: `0x01456EEC` PortalFadeLeaveBiome, `0x01456EF0` PortalFadeEnterBiome,
`0x01456EF4` Fade, `0x01456EF8` CancelFade.

| Function | Effect |
|---|---|
| `FUN_008DC780(this, leave)` | leave=1: `+0x139 = 1`, plays LeaveBiome/Fade (fade to white). leave=0: `+0x138 = 1`, plays EnterBiome/Fade (fade out of white) |
| `FUN_008DC810` | if `+0x139`: clear it, play LeaveBiome/CancelFade |
| `FUN_008DC8D0` | "leave fade finished", one of the teleport readiness checks in `FUN_00964010` |

Opened (id 0x42) by the Enter functions of two game states, `FUN_0080E730` and `FUN_00810410`,
right before they call the gameplay Enter.

### Who drives it: the local player entity **[code]**

The player entity class (vtable `0x012D2344`, ctor `FUN_0086B7F0`) has request fields
`+0x91` (fade requested), `+0x92` (leave flag), `+0x93` (cancel requested) and timestamp `+0x88/+0x8C`.

- Tick `FUN_0086BD40`: if `+0x91` and the time is due, `FUN_008DC780(+0x92)`. If `+0x93`, `FUN_008DC810`.
- Entity events `FUN_0086B9C0(id)`: `0x23` requests a fade, `0xA4` requests a leave-fade (`+0x91/+0x92 = 1/1`),
  `0x57` requests a cancel.
- Parameters changed `FUN_0086B8A0(list)`: binding 17 (`islocal`) fires event `0xB` and, when local, sets `+0x91 = 1, +0x92 = 0`
  (EnterBiome, fade out of white).
- **All three return immediately unless `+0x81 & 0x08`.**

`+0x80` low 12 bits hold the bool params of `clientplayeraspectscomponent` in binding order
(bit 5 = isspectator ... bit 11 = **islocal**). The value `0xFFFFF000` was read live. `+0x50` = teleportingmethod.

The server never sent `islocal`, so the bit stayed clear, so nothing ever faded the white out.

## Fix (server)

0. **TeleportBegins is off** (`EnteredGameplay.SendTeleport = false`). No reconnect anymore. Without it
   `ui_loading_screen` (0x45) closes by itself **[run]**, so the `+0xFC4` gate above does not matter for the white screen.

1. Live proof **[run]**: writing `+0x81 |= 8` and `+0x91 = 1` with WriteProcessMemory lifted the white at once.
2. `PlayerAspectsComponent.IsLocal` + `Entity.GetSyncData(forOwner, onlyParameter)`: the owner alone gets `islocal = true`.
3. Five `EntitySync` copies (islocal only), 2/4/6/8/10 s after EnteredGameplay, sent from `PlayerConnection.Tick`
   on the server thread. Sending from a `Task.Delay` thread raced `ProcessMaps` over `SyncingForOwner` and sent `false`.
   **[run]** Whether a single copy at 2 s lands varies from run to run. The first accepted copy lifts the fade, and the later ones are ignored.
   **Open:** about 4 s after accepting it, the client clears islocal by itself. EntityMoved packets stop at that moment, and resending `true` does not set it again.
   The white does not come back, but local-player control is lost.
   **[run]** A copy sent in the same tick as EnteredGameplay was ignored; the delayed one applied
   (flags `0xF0 -> 0xF8`, portalfade `+0x138 = 1`, frame average 250 -> ~20). The delay is a stand-in until the real trigger is found.
4. Not in the initial EntityAdd: there it crashed the client in READY_TO_PLAY with
   `STATUS_FLOAT_MULTIPLE_TRAPS` at `0x00D45EDC` (zero duration into `FUN_00D45E60` via `FUN_00BE0540`).
   That crash is still open; it means some local-player timer data the server should send is missing.

## Dead ends, kept so nobody repeats them

- `FUN_00715E10` does not wipe `+0xFC4` during the reconnect: the spec stayed VALID across it. **[run]**
- `FUN_008F7B20` (FadeOff/FadeOn/ResetToDefault) is the loading-screen photo slideshow, not its close.
- The `0x45` that `FUN_00964010` uses was misread at first as a value slot. It is a UI screen id.
