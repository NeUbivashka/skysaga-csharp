---
name: skysaga-server-conventions
description: How to build, run, change and commit the SkySaga Alpha 10 emulator (Servers/Alpha10 in skysaga-csharp) - services and ports, packet handlers and senders, entity components, GeoData-derived tables and their generators, PostgreSQL migrations, chat commands, testing and commit rules. Use before writing or changing any server code in this repository.
---

# SkySaga Alpha 10 server conventions

The active emulator is `Servers/Alpha10` (client 36731, .NET 10). `Servers/Beta1` (client 38328)
is dropped; do not change it. The client's behaviour is the specification: reverse it first
(skill `skysaga-client-reversing`), document it (skill `skysaga-wiki-writing`), then implement.

## Rules

- **No code comments.** Explanations go to the wiki. Write code that reads like the surrounding
  code: same naming, same idioms, same `Console.WriteLine("[Tag] …")` logging.
- **No workarounds.** Fix the root cause. Do not resend, refresh or toggle state to hide a bug.
- **No invented constants.** Values come from the client or GeoData. If the server must decide
  something the client does not tell, keep it small and mark it in the wiki as an emulator choice.
- **Data over code.** Game rules read GeoData-derived tables (`Data/*.txt`), not hard-coded
  names or numbers.
- **Test in game before committing.** A change is committed only after a player has checked it
  in the real client. Say what was tested.
- **Never commit secrets.** That covers `Data/database.local.json`, `Data/account.json`, the
  `auth-keys` directory, tokens from logs, `bin/` and `obj/`.

## Projects

| Project | Role |
|---|---|
| `SkySaga.Game` | Game server: RakNet on UDP **42069**, entities, components, packets, worlds, chat IRC on **444** |
| `SkySaga.Auth` | ASP.NET web API and account site on **5100**: login, characters, worlds, social graph, photos, conductor |
| `SkySaga.Game/server.py` | Sniffing proxy on **5001** (HTTP backend 5080, HTTPS 5443). Answers the remaining client web calls and forwards `/api/authentication`, `/api/account`, `/api/persistent-record/characters`, `/api/social-graph/` … to SkySaga.Auth |
| `SkySaga.Admin` | Blazor admin panel on **5280** |
| `SkySaga.Data` | EF Core model and migrations (PostgreSQL), shared by Game, Auth and Admin |
| `SkySaga.RakNet` | RakNet port: `BitStream` and the wire primitives |
| `SmilegateAuth`, `SkySaga.Web` | Old services, not needed for Alpha 10 |

All four services must run for the client to get in game. Build with
`dotnet build Servers/Alpha10/Servers.sln`. Start the game server from its output folder with
the uniform data mode the 36731 client expects:

```bash
cd Servers/Alpha10/SkySaga.Game/bin/Debug/net10.0
SKYSAGA_DATA2_UNIFORM=fd ./SkySaga.Game.exe
```

Restarting servers is fine. Do not restart or kill the player's game client without asking.

## Packets

Ordinals live in `SkySaga.Game/PacketId.cs`, in the client's order (wiki RPC-Ordinals). Wire
ids: `0x86 + ordinal` for 0–120, otherwise `FF` then `ordinal − 121` (`BitStream.WritePacketId`).

**Client → server.** Use a static class in `Packets/` with
`public static bool Handle(PlayerConnection connection, BitStream bitStream)`. Return `false` when
the payload cannot be read. Add one dispatch line in `Managers/Player/PlayerConnection.cs`:

```csharp
PacketId.AllowFriendToEditHomeIsland => AllowFriendToEditHomeIsland.Handle(this, bitStream),
```

Read fields in the exact order and widths of the client's send stub. Use the `Read*`
`out` pattern (`ReadClientUuid`, `ReadInt32(max, out …)` with the client's range maximum, `ReadFullWidthUInt32` …).

**Server → client.** Use a class implementing `ISerializablePacket` (`BitStream Serialize()`)
with public fields and, when useful, a static factory. Send it with `connection.Send(packet)`, or
broadcast through the world's connections.

```csharp
bitStream.WritePacketId(PacketId.TimeSync);
bitStream.WriteUInt64(GameTimeMs);
bitStream.WriteRanged(Math.Min(TimeScale, TimeScaleMax), TimeScaleMax);
bitStream.WriteUInt64(RealTimeMs);
```

Ranged values take `32 − clz(max)` bits, so the `max` must be the client's. The other
primitives are `WriteClientUuid`, `WriteEscapedCount`, `InventoryItemRef.WriteOptionalUInt32`
and the string writer.

## Components and sync

Entity components are in `SkySaga.Game/Components`, one class per client component
(`Client*Component`). A synced property uses
`public T Name { get; set => SetIfChanged(ref field, value); }`. A change raises
`ParameterChanged`, and the entity sync sends the field to the clients. Use `ChangedForOthers`
for fields only other players need. Field order and encodings are documented in wiki
Entity-Sync and must match the client's reader.

## GeoData tables

Client data is in `Bundled/36731` (`GeoData.json`, `Entities.json`). The server does not parse
GeoData at runtime. Python generators in `Tools/maps` extract what it needs into
`SkySaga.Game/Data/*.txt|json`. A static `*Table` class with a `Lazy<>` loader reads each file
(`PerkTable`, `CombatStatTable`, `CreatureAiTable` …). New data files need a `<None Update=…>`
copy entry in `SkySaga.Game.csproj`.

The generators default to Beta 1. Always run them for Alpha 10, and without `python -I`,
because they import `targets.py` from their folder:

```bash
SKYSAGA_TARGET=alpha10 python Tools/maps/make_combat_stats.py
```

Check `git diff --stat` afterwards: only `Servers/Alpha10/SkySaga.Game/Data` should change.

| Generator | Output |
|---|---|
| `make_combat_stats.py` | `CombatStats.txt` (health, weapon/armour level, knockback per item and creature) |
| `make_perks.py` | `Perks.txt` |
| `make_creature_ai.py` | `CreatureAi.txt` |
| `make_loot_tables.py`, `make_voxel_loot.py` | `LootTables.txt`, `VoxelLoot.txt` |
| `make_recipes.py`, `make_jobs.py`, `make_job_challenges.py` | `Recipes.txt`, `Jobs.txt`, `JobChallenges.*` |
| `make_tool_stats.py`, `make_action_stats.py`, `make_action_list_*.py` | tool, action and action list tables |
| `make_devices.py`, `make_stack_limits.py`, `make_two_handed.py`, `make_item_sort.py` | item and device tables |
| `make_editor_data.py` | data for `Tools/terrain-editor` |

GeoData refers to names by CRC32 (`Util.ComputeCrc32`, lower-cased name). Conditions
(`GameModeIs`, `ConditionOrList` …) that the client evaluates must be evaluated the same way
when a generator bakes a value. Example: armour perks depend on the game mode, which the server
sends per world (`WorldInfo.GameMode`).

## Database

PostgreSQL through EF Core in `SkySaga.Data`. Entities go in `Entities/`; migrations go in
`Migrations/` with the `[Migration("yyyyMMddHHmmss_Name")]` id, a `.Designer.cs`, and an updated
`SkySagaDbContextModelSnapshot.cs`. Create them with the local tool
(`dotnet tool restore`, then `dotnet ef migrations add <Name>`).

- **Timestamps.** Keep migration ids ordered after the newest existing one.
- **Applying.** The game server and SkySaga.Auth apply migrations on start
  (`Database.Migrate()`).
- **Connection settings.** They are read from `Data/database.local.json`, which is never
  committed or quoted.

## Worlds and players

- **Worlds.** `Managers/World` holds the world directory and instances: islands, the hub,
  character creation, FTUE and adventures. A world's kind decides its game mode, visit and build
  rights (`WorldInfo`).
- **Players.** `Managers/Player` holds the connection, initialisation, saves, inventory,
  crafting and jobs.
- **Character identity.** The character comes from RPC 86 (`SetUserToken`) and the account
  session. Never assume a single player.

## Chat commands

Debug commands are in `SkySaga.Game/ChatCommands.cs` (`!heal`, `!kill`, `!tp`, `!give`, `!spawn`,
`!time`, `!xp` …). Add one only when it helps testing. Remove commands that can crash the client.

## Commits

- **Branch.** Push straight to `main` after `git pull --rebase --autostash`. No pull requests
  for maintainers.
- **Message.** A short subject, then bullets saying what changed and what was tested.
- **Credit.** Credit contributors whose code was taken with a `Co-Authored-By` line.
- **Scope.** Commit only finished, tested work. Leave unrelated or unfinished hunks out
  (`git add -p`).
- **Order.** Server and wiki changes go in the same session; the wiki repository is committed
  separately.
