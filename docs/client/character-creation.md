# Character creation and the new-player flow (36731)

How a new character is meant to reach the creator, from GeoData.json and the client code.

## Intended flow

1. **FTUE_The_Old_City** (Adventures[78], wire 79, `IsFTUE: true`): the scripted tutorial. It has
   an intro video, cutscenes, and movement/attack unlocked by cutscene `OnCompleteActions`.
2. **DestinationSelection** (Adventures[64], wire 65, WorldType `DestinationSelection`, RootFeature
   `CharacterSelect`, palette `BiomePalette_Forest_Traditonal`, ToD `MidDay_Fixed`, MapSize
   `HomeIsland_6x4x6`): the airship. The character is created here.
3. **Home island / social hub**. `Airship_Leave_To_CoFL` (AnimatedSequences[2]) ends with
   `TeleportToWorld { DestinationType: SocialHub }`.

## From the airship to the creator

```
DestinationSelection.AdventureStartedClientActions
  -> DestinationSelectionIntro            Actions[27], ConditionalAction on HasQuestFromSource
       (false) -> Trigger_CharacterCreateIntro   Actions[160], TriggerAnimSequence
                  CC_EquipHelmet
  -> Character_Create_Intro               AnimatedSequences[4], prop PilotHelmet_CC
       OnCompleteActions: Action_ShowCharacterCustomisation, UnequipHelmet
  -> Action_ShowCharacterCustomisation    Actions[3], RunMeandrosAction (ExportedActions[3])
  -> faGame_ShowCharacterCustomisation    script function FUN_01207B60
```

`FUN_01207B60` gets the local player and its `ClientCharacterCustomisationComponent`. It switches
to game state 0xE (GameState_CharacterCreation) when `IsCharacterCustomised` is 0 (component
`+0x68` bit 0) and the `useusername` option is off.

Then come `Character_Create_Complete` (AnimatedSequences[3], prop PaperMap_CC) and
`Character_Create_Post_Loop` (5).

## Game and frontend states

The game state machine is the object at `ModeGlobal + 0xCE0`; its state pointers start at slot
0x338. The switch is FUN_00718D00(id), and the current id is at `ModeGlobal + 0xD34`.

| id | state |
|----|-------|
| 0x9 | GameState_Gameplay |
| 0xD | GameState_ReadyToPlay |
| 0xE | GameState_CharacterCreation (ctor FUN_00810290, enter FUN_00810410, vtable 0x012C2D38) |

The frontend machine is at `ModeGlobal + 0xD38`, with 12 states switched by FUN_00718B90(id):

| id | state |
|----|-------|
| 3 | DownloadCharacterList |
| 5 | CharacterCreation (not reached in 36731) |
| 8 | SelectCharacter |
| 0xB | JoinHomeworld |

DownloadCharacterList always goes to SelectCharacter, and SelectCharacter always goes to
JoinHomeworld. In this build the frontend never creates a character; creation happens in game.

GameState_CharacterCreation enter (FUN_00810410) opens UI screens 0x42 (`ui_portalfade`), 0x6B
(`ui_animatedPrompt_v3`) and 0x35 (`ui_accountSetup_Back`).

## Saving

1. The client checks the name: `/api/account/name/checkName`. The reply is read by FUN_00810C00
   for the keys `valid`, `profane`, `containsNotAllowedCharacters`, `alreadyExists` and
   `suggestion`.
2. If the name is valid, it sends **RPCSaveCharacterName** (162, C2S, stub FUN_008113B0) and waits.
3. The server answers **RPCCharacterCreationResponse** (163, S2C, reader FUN_007EDE70, one ranged
   field, max 4). FUN_007150E0 forwards it to FUN_00810710 only while the game state is 0xE.
   Result 0 continues; result 1 logs "Character Saved Unsuccessful".

The appearance is sent as **RPCSetCharacterCustomisationData** (53, C2S, stub FUN_00B21A10):
a 32-bit value, then the struct written by FUN_00B22A00.

## clientcharactercustomisationcomponent (Player)

Factory FUN_00A8EAA0 (0x78 bytes) → ctor FUN_00B21620 (vtable 0x01331248) → base FUN_00BF9620
(vtable 0x01340F10). The bindings sub-object is at `+0x20` (vtable 0x01340EF0); its read function
is FUN_00BF9D60. Name → index: FUN_00A9E970.

| binding | name | reader | wire |
|---------|------|--------|------|
| 0 | CustomisationData | FUN_00BFA830 | struct, see below |
| 1 | CustomisationListCRC | FUN_007EA4B0 | optional u32 (presence bit + 32 bits) |
| 2 | ScaleRangeCRC | FUN_007EA4B0 | optional u32 |
| 3 | IsCharacterCustomised | bit | bool, `+0x48` bit 0 of the bindings object |
| 4 | InitialSignatureColour | FUN_00BF9AB0 | u16 (not in Entities.json) |

Player sync indices: customisationdata 37, customisationlistcrc 38, ischaractercustomised 83
(scalerangecrc is also a binding).

### CustomisationData

Field names from the client's own RPC log. Wire order is the order FUN_00BFA830 reads in:

| field | reader | wire | example |
|-------|--------|------|---------|
| Gender | ranged, max 2 | 2 bits | `0` male, `1` female |
| Tribe | FUN_00BF9B30 | ranged index (u16) | `Human`, `Cat`, `Lizard`, `Monkey` |
| MaterialList | FUN_00BFA000 | list of material indices | `["Human_Skin_Pink1","Human_Eyes_Dark_Brown","Fabric_Yellow"]` (skin, eyes, clothes) |
| AttachmentList | FUN_00BFA4C0 | list of {Attachment, Material} | 5 slots: hairstyle, ears/extra, beard, …; `""` = empty |
| MaterialCustomisationList | FUN_00BFA260 | list | 9 slots: markings ×3, pattern palette, 3 empty, eye, mouth |
| Backstory | FUN_00BF9A30 | ranged index (u16) | `""` |

Example (logged by the client):

```json
{"entityID":9,"customisationData":{"Gender":0,"Tribe":"Human",
 "MaterialList":["Human_Skin_Pink1","Human_Eyes_Dark_Brown","Fabric_Yellow"],
 "AttachmentList":[{"Attachment":"HumanMaleHairstyle00_Bald","Material":"Human_Hair_Brown"},
  {"Attachment":"","Material":"Human_Hair_Brown"},
  {"Attachment":"HumanMaleBeard01_None","Material":"Human_Hair_Brown"},
  {"Attachment":"","Material":"Human_Hair_Brown"},{"Attachment":"","Material":"Human_Hair_Brown"}],
 "MaterialCustomisationList":["Pattern_HumanMaleMarkings00_None","Pattern_HumanMaleMarkings00_None",
  "Pattern_HumanMaleMarkings00_None","PatternPalette_Human_01","","","",
  "Eye_Human_Male02_Cute","Mouth_Human_Male02_Cute"],"Backstory":""}}
```

The creator sends this on every change (tribe, gender, colour), not only at the end. The RPC
payload is 264 bits: the 32-bit entity id, then this struct.

## Server status

- `SKYSAGA_ADVENTURE=ftue|airship|home` selects MapSpec.Ftue / DestinationSelection / HomeIsland.
- The intro video is sent only for the FTUE.
- The web stub answers checkName with `valid: true` (at the root and inside `result`).
- SetCharacterCustomisationData: the 231-bit struct is kept on the connection.
- SaveCharacterName (a string, read after the escaped ordinal): the server sets Playername,
  CustomisationData and IsCharacterCustomised = 1, replies CharacterCreationResponse(0) and saves.
  Verified 2026-10-12: the client leaves the creator and stays in Gameplay. Before
  IsCharacterCustomised was synced it looped Gameplay ↔ CharacterCreation.
- `ClientCharacterCustomisationComponent` syncs the raw struct, absent CRCs and the flag. All of
  it is persisted in the player save (`IsCharacterCustomised`, `CustomisationData` as base64).
  Synced by default since 2026-10-12. The earlier crash (FUN_00C18D00) was the shifted Player
  stream, not the struct. With the third-person camera and equipped armour, the created character
  (MiNo) shows in the world with its hair and colours.
- RPCJoinDestinationSelectionWorld (135, C2S, `{"triggeringEntityID":5}`, sent when the player uses
  the airship map) is parsed and logged, not acted on yet.
