# AGENTS.md — skysaga-csharp

Operating rules for AI agents working in this repository. Read before any change.

## What this is

C# server for **SkySaga: Infinite Isles** emulation, forked from [EDITzDev](https://github.com/EDITzDev)'s R&D repo. Targets client build **36731** (alpha 10.6.6). .NET 10.

This server is the **wire oracle** — it speaks the protocol the real client expects. The Go reimplementation (`skysaga-go`) must match it byte-for-byte.

## Project structure

```
Servers/
  Servers.sln                 Solution file
  SkySaga.Game/               Game server logic (packets, entities, components, managers)
  SkySaga.Web/                HTTP API (login, auth endpoints)
  SkySaga.RakNet/             RakNet C# port (bitstream, connection, file transfer)
  SkySaga.ChatCommunication/  Chat service
  SmilegateAuth/              Auth service
Tools/
  PatchedLaunch/              Client launcher patcher
  AudioExtractor/             Game audio extraction tool
Bundled/                      Game data bundles (builds 10414, 20015, 25516, 27094, 30968, 36546, 36731)
```

## Build

```bash
cd Servers
dotnet build Servers.sln
```

Individual projects:

```bash
dotnet build Servers/SkySaga.Game/SkySaga.Game.csproj
dotnet build Servers/SkySaga.Web/SkySaga.Web.csproj
```

## Run

```bash
cd Servers/SkySaga.Web
dotnet run
```

## Golden rules

1. **This is a fork, not upstream.** Preserve EDITzDev's original structure. Changes are additive or fixing.
2. **Wire protocol is king.** Packet ordinals, bit layouts, and field order match the real client. Never change a packet format without confirming against the live client.
3. **RakNet variant: Windows-compatible baseline.** This repo ships the unmodified RakNet C# port. Sergei has a separate modified variant (SWIG wrappers) for the Go server — that lives in `skysaga-go`, not here.
4. **Lёva (NeUbivashka) has write access.** He works on this repo directly. Coordinate changes to avoid conflicts.
5. **No co-author / attribution trailers in commits.** Clean messages only.
6. **Do not commit** build artifacts (`bin/`, `obj/` — already in .gitignore), secrets, or .env files.

## Branching

- `main` is the default branch.
- Feature branches: `feat/<scope>`, `fix/<scope>`.
- PR to merge. Keep branches short-lived.
- Documentation-only changes may go directly to `main`.

## Key files

- `Servers/SkySaga.Game/PacketId.cs` — packet ordinal definitions
- `Servers/SkySaga.Game/Packets/` — packet handlers (encoders/decoders)
- `Servers/SkySaga.RakNet/BitStream.cs` — RakNet bitstream (wire primitives)
- `Servers/SkySaga.Game/Server.cs` — main game server loop
- `Bundled/36731/` — game data for the target client build

## Relationship to other repos

- **skysaga-go** (Go server) reads this repo's packet code as reference for wire-format porting.
- **skysaga-re** (RE workspace) confirms/corrects protocol behaviour via Frida/Ghidra against the live client.
- This repo does NOT depend on the others — it's self-contained.

## Client version note

README mentions client 10414; the actual target is **36731** (alpha 10.6.6). Bundled/36731 has the correct game data. The README is from upstream EDITzDev and hasn't been updated.
