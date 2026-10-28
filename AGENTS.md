# AGENTS.md — skysaga-csharp

Operating rules for AI agents working in this repository. Read before any change.

## What this is

C# server for **SkySaga: Infinite Isles** emulation, forked from [EDITzDev](https://github.com/EDITzDev)'s R&D repo. Targets client build **36731** (alpha 10.6.6). .NET 10.

This server is the **wire oracle** — it speaks the protocol the real client expects. The Go reimplementation (`skysaga-go`) must match it byte-for-byte.

## Project structure

```
Servers/
  Alpha10/                    Emulator for Alpha 10 build 36731 (active, all current work)
  Beta1/                      Emulator for Beta 1 build 38328 (dropped, not maintained), same layout as Alpha10:
  Servers.sln                 Solution file
  SkySaga.Game/               Game server (RakNet :42069, chat IRC :444), packets, entities, components, worlds; server.py web proxy (:5001)
  SkySaga.Auth/               Web API and account site (:5100): login, characters, worlds, social graph, photos
  SkySaga.Admin/              Admin panel (:5280)
  SkySaga.Data/               EF Core model and PostgreSQL migrations
  SkySaga.RakNet/             RakNet C# port (bitstream, connection, file transfer)
  SkySaga.Web/, SmilegateAuth/, SkySaga.ChatCommunication/  Old services, not used by Alpha 10
Tools/
  maps/                       GeoData → server table generators (run with SKYSAGA_TARGET=alpha10)
  terrain-editor/             Map editor (one HTML file)
  PatchedLaunch/              Client launcher patcher
  AudioExtractor/             Game audio extraction tool
.claude/skills/               Agent skills: client reversing, wiki writing, server conventions
Bundled/                      Game data bundles (builds 10414, 20015, 25516, 27094, 30968, 36546, 36731, 38328)
```

## Build

```bash
cd Servers/Alpha10
dotnet build Servers.sln
```

## Run

All of these must run for the client to get in game:

```bash
cd Servers/Alpha10/SkySaga.Game/bin/Debug/net10.0 && SKYSAGA_DATA2_UNIFORM=fd ./SkySaga.Game.exe
cd Servers/Alpha10/SkySaga.Auth && dotnet run
cd Servers/Alpha10/SkySaga.Game && python server.py
cd Servers/Alpha10/SkySaga.Admin && dotnet run
```

PostgreSQL connection settings are read from `SkySaga.Game/Data/database.local.json` (never committed).

## Skills

Before changing the server, read the skills in `.claude/skills/`:

- `skysaga-client-reversing`: how to confirm a packet, field or rule against the 36731 client (Ghidra bridge, `scripts/ghidra_tools.py`).
- `skysaga-wiki-writing`: how findings are written to the GitHub wiki.
- `skysaga-server-conventions`: projects, packets, components, data tables, migrations, testing and commits.

## Golden rules

1. **This is a fork, not upstream.** Preserve EDITzDev's original structure. Changes are additive or fixing.
2. **Wire protocol is king.** Packet ordinals, bit layouts, and field order match the real client. Never change a packet format without confirming against the live client.
3. **RakNet variant: Windows-compatible baseline.** This repo ships the unmodified RakNet C# port. Sergei has a separate modified variant (SWIG wrappers) for the Go server — that lives in `skysaga-go`, not here.
4. **Maintainers:** NeUbivashka and v1k1ng31 push directly; external contributors work via fork + PR (see CONTRIBUTING.md).
5. **No AI attribution trailers in commits.** A `Co-Authored-By` line is used only to credit a human contributor whose code was taken.
6. **Do not commit** build artifacts (`bin/`, `obj/` — already in .gitignore), secrets, or .env files.

## Branching

- `main` is the default branch.
- Maintainers commit straight to `main` (`git pull --rebase` first), and only after the change was tested in the real client.
- External contributors use a fork, a `feat/<scope>` or `fix/<scope>` branch and a PR (see CONTRIBUTING.md).

## Key files

- `Servers/Alpha10/SkySaga.Game/PacketId.cs` — packet ordinal definitions
- `Servers/Alpha10/SkySaga.Game/Packets/` — packet handlers (encoders/decoders)
- `Servers/Alpha10/SkySaga.RakNet/BitStream.cs` — RakNet bitstream (wire primitives)
- `Servers/Alpha10/SkySaga.Game/Server.cs` — main game server loop
- `Bundled/36731/` — game data for the target client build

## Relationship to other repos

- **skysaga-go** (Go server) reads this repo's packet code as reference for wire-format porting.
- **skysaga-re** (RE workspace) confirms/corrects protocol behaviour via Frida/Ghidra against the live client.
- This repo does NOT depend on the others — it's self-contained.

## Client version note

The target is client **36731** (alpha 10.6.6); `Bundled/36731` has its game data. Work on Beta 1 (38328) was dropped. Protocol and client documentation is in the GitHub wiki; server code carries no comments.
