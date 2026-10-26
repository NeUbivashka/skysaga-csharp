# SkySaga C# Server

Research & development server emulator for **[SkySaga: Infinite Isles](https://wikipedia.org/wiki/SkySaga:_Infinite_Isles)**, written in C# (.NET 10).

Fork of [EDITzDev/SkySaga](https://github.com/EDITzDev/SkySaga), maintained by [@NeUbivashka](https://github.com/NeUbivashka) and [@v1k1ng31](https://github.com/v1k1ng31). Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

Protocol and gameplay documentation lives in the [Wiki](../../wiki).

> This is a non-commercial preservation / research project. It is not affiliated with Radiant Worlds or Smilegate. You need your own copy of the game client.

## Supported client builds

| Folder | Build | Status |
|---|---|---|
| `Servers/Alpha10` | 36731 (alpha 10.6.6) | active: all current work |
| `Servers/Beta1` | 38328 (beta 1) | dropped: not maintained, kept for reference |

## Repository layout

```
Servers/<Build>/
  Servers.sln                 solution
  SkySaga.Game/               game server (packets, entities, components, managers); Data/ = server tables
  SkySaga.Web/                HTTP API (login, auth, geonode)
  SkySaga.RakNet/             RakNet C# wrapper + RakNet.dll
  SkySaga.ChatCommunication/  chat (IRC)
  SmilegateAuth/              auth service
Bundled/                      data bundled with SkySaga.exe per build (see Bundled/README.md)
Tools/                        client launcher patcher, audio extractor, table generators, RE scripts
docs/                         protocol and client notes
```

## Build & run

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd Servers/Alpha10
dotnet build Servers.sln
```

### Database

The game server keeps accounts, characters, inventories, worlds, voxel edits, placed devices and an event log in
PostgreSQL (EF Core, schema in `Servers/Alpha10/SkySaga.Data`). It applies migrations itself on start and will not start
without a database.

1. Install PostgreSQL (the Windows installer from postgresql.org is fine).
2. Create a role and a database for the server, for example:

   ```sql
   CREATE ROLE skysaga LOGIN PASSWORD '<password>';
   CREATE DATABASE skysaga OWNER skysaga ENCODING 'UTF8' TEMPLATE template0;
   ```

3. Give the server the connection string, either in the `SKYSAGA_DB` environment variable or in
   `Servers/Alpha10/SkySaga.Game/Data/database.local.json` (ignored by git):

   ```json
   { "ConnectionString": "Host=127.0.0.1;Port=5432;Database=skysaga;Username=skysaga;Password=<password>" }
   ```

On the first start the old JSON saves (`Data/Saves/player.json`, `voxel_edits_*`, `devices_*`, `checkpoints_*`) are
imported and renamed to `*.imported`.

### Admin panel

`Servers/Alpha10/SkySaga.Admin` is a Blazor web panel over the same database: overview with charts, players (profile,
inventory, jobs, bans), worlds and devices, event log. It reads the connection the same way as the game server.

```bash
dotnet run --project Servers/Alpha10/SkySaga.Admin
```

It listens on http://127.0.0.1:5280 only; it has no login yet.

- `SkySaga.Game` P/Invokes `RakNet.dll` (Windows PE), so the game server currently runs on **Windows only**.
- `SkySaga.Web` and `SmilegateAuth` also run on Linux; a container stack is provided:

```bash
./scripts/gen-cert.sh                      # self-signed cert for the Caddy front door
SKYSAGA_GAME_IP=<address reachable by the client> podman-compose up -d --build
```

See `docker-compose.yml` for ports and environment variables.

## Links

- [Upstream: EDITzDev/SkySaga](https://github.com/EDITzDev/SkySaga)
- [SkySaga Resource Explorer](https://github.com/EDITzDev/SkySagaResourceExplorer)
- [SkySaga Archive](https://etithespir.it/skysaga)
- [SkySaga Reverse Engineering Discord](https://discord.gg/KeaytU2nud)

## License

[MIT](LICENSE). Game data in `Bundled/` and `Servers/*/SkySaga.Game/Data/` is derived from the original game and belongs to its respective owners.
