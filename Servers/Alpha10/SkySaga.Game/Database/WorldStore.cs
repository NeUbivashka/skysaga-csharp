using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;

namespace SkySaga.Game.Database;

public static class WorldStore
{
    private static readonly Dictionary<string, Guid> WorldIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock WorldIdsLock = new();

    public static string MapName => Path.GetFileNameWithoutExtension(WorldMapLoader.MapFileName);

    public static Guid WorldId()
    {
        var mapName = MapName;

        lock (WorldIdsLock)
        {
            if (WorldIds.TryGetValue(mapName, out var known))
                return known;

            using var context = GameDatabase.Context();
            var world = context.Worlds.FirstOrDefault(w => w.MapName == mapName);

            if (world is null)
            {
                world = new World { Id = Guid.NewGuid(), MapName = mapName, CreatedAt = DateTimeOffset.UtcNow };
                context.Worlds.Add(world);
                context.SaveChanges();
                Console.WriteLine($"[WorldStore] Created world {mapName} ({world.Id})");
            }

            WorldIds[mapName] = world.Id;
            return world.Id;
        }
    }

    public static List<(int X, int Y, int Z, byte Block)> LoadVoxelEdits()
    {
        var worldId = WorldId();
        using var context = GameDatabase.Context();

        return [.. context.VoxelEdits.AsNoTracking()
            .Where(e => e.WorldId == worldId)
            .Select(e => new { e.X, e.Y, e.Z, e.Block })
            .AsEnumerable()
            .Select(e => (e.X, e.Y, e.Z, (byte)e.Block))];
    }

    public static void ImportVoxelEdits(IEnumerable<(int X, int Y, int Z, byte Block)> edits)
    {
        var worldId = WorldId();
        using var context = GameDatabase.Context();

        context.VoxelEdits.Where(e => e.WorldId == worldId).ExecuteDelete();
        context.VoxelEdits.AddRange(edits.Select(e => new VoxelEdit { WorldId = worldId, X = e.X, Y = e.Y, Z = e.Z, Block = e.Block }));
        context.SaveChanges();
    }

    public static void SaveVoxelEdit(int x, int y, int z, byte block)
    {
        var worldId = WorldId();

        GameDatabase.Enqueue(context => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO voxel_edits (world_id, x, y, z, block) VALUES ({worldId}, {x}, {y}, {z}, {(short)block})
            ON CONFLICT (world_id, x, y, z) DO UPDATE SET block = excluded.block
            """));
    }

    public static List<PlacedDevice> LoadDevices()
    {
        var worldId = WorldId();
        using var context = GameDatabase.Context();

        return [.. context.PlacedDevices.AsNoTracking().Where(d => d.WorldId == worldId).OrderBy(d => d.Id)];
    }

    public static void SaveDevices(IEnumerable<PlacedDevice> devices, bool wait = false)
    {
        var worldId = WorldId();
        var snapshot = devices.Select(d => new PlacedDevice
        {
            WorldId = worldId,
            ItemCrc = d.ItemCrc,
            X = d.X,
            Y = d.Y,
            Z = d.Z,
            OwnerCharacterId = d.OwnerCharacterId,
            Materials = d.Materials,
            Items = d.Items
        }).ToList();

        async Task Write(Data.SkySagaDbContext context)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.PlacedDevices.Where(d => d.WorldId == worldId).ExecuteDeleteAsync();
            context.PlacedDevices.AddRange(snapshot);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        if (!wait)
        {
            GameDatabase.Enqueue(Write);
            return;
        }

        using var context = GameDatabase.Context();
        Write(context).GetAwaiter().GetResult();
    }

    public static Dictionary<Guid, float[]> LoadCheckpoints()
    {
        var worldId = WorldId();
        using var context = GameDatabase.Context();

        return context.Checkpoints.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .ToDictionary(c => c.CharacterId, c => new[] { c.X, c.Y, c.Z });
    }

    public static void SaveCheckpoint(Guid characterId, float x, float y, float z)
    {
        var worldId = WorldId();

        GameDatabase.Enqueue(context => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO checkpoints (world_id, character_id, x, y, z) VALUES ({worldId}, {characterId}, {x}, {y}, {z})
            ON CONFLICT (world_id, character_id) DO UPDATE SET x = excluded.x, y = excluded.y, z = excluded.z
            """));
    }

    public static void ImportCheckpoints(Dictionary<Guid, float[]> checkpoints)
    {
        var worldId = WorldId();
        using var context = GameDatabase.Context();

        foreach (var (characterId, position) in checkpoints)
            if (position.Length == 3)
                context.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO checkpoints (world_id, character_id, x, y, z) VALUES ({worldId}, {characterId}, {position[0]}, {position[1]}, {position[2]})
                    ON CONFLICT (world_id, character_id) DO UPDATE SET x = excluded.x, y = excluded.y, z = excluded.z
                    """);
    }

    public static void MarkImported(string path)
    {
        File.Move(path, path + ".imported", overwrite: true);
        Console.WriteLine($"[WorldStore] Imported {path} into the database.");
    }
}
