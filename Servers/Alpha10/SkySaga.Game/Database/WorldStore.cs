using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Game.Database;

public static class WorldStore
{
    public const uint DefaultSeed = WorldCatalog.DefaultSeed;

    public static WorldInfo? Load(Guid worldId)
    {
        using var context = GameDatabase.Context();
        var world = context.Worlds.AsNoTracking().SingleOrDefault(w => w.Id == worldId);

        return world is null ? null : ToInfo(context, world);
    }

    public static WorldInfo Hub()
    {
        using var context = GameDatabase.Context();
        return ToInfo(context, WorldCatalog.Hub(context, TemplateStore.DataDirectory));
    }

    public static WorldInfo HomeIsland(Guid characterId)
    {
        using var context = GameDatabase.Context();
        return ToInfo(context, WorldCatalog.HomeIsland(context, TemplateStore.DataDirectory, characterId));
    }

    public static WorldInfo Instance(WorldKind kind, Guid ownerCharacterId)
    {
        using var context = GameDatabase.Context();
        var ownerName = context.Characters.Where(c => c.Id == ownerCharacterId).Select(c => c.Name).SingleOrDefault();

        return new WorldInfo(Guid.NewGuid(), kind, WorldCatalog.Template(TemplateStore.DataDirectory, kind), null, DefaultSeed, ownerCharacterId, ownerName,
            WorldAccess.Owner, WorldAccess.Owner);
    }

    public static (Guid? WorldId, bool Customised)? Character(Guid characterId)
    {
        using var context = GameDatabase.Context();
        var character = context.Characters.AsNoTracking().Where(c => c.Id == characterId)
            .Select(c => new { c.CurrentWorldId, c.IsCustomised }).SingleOrDefault();

        return character is null ? null : (character.CurrentWorldId, character.IsCustomised);
    }

    public static void SetCurrentWorld(Guid characterId, Guid worldId)
    {
        using var context = GameDatabase.Context();
        context.Characters.Where(c => c.Id == characterId)
            .ExecuteUpdate(update => update.SetProperty(c => c.CurrentWorldId, worldId));
    }

    public static void Visited(Guid worldId)
    {
        var now = DateTimeOffset.UtcNow;

        GameDatabase.Enqueue(context => context.Worlds.Where(w => w.Id == worldId)
            .ExecuteUpdateAsync(update => update.SetProperty(w => w.LastVisitedAt, now)));
    }

    private static WorldInfo ToInfo(Data.SkySagaDbContext context, World world)
    {
        var ownerName = world.OwnerCharacterId is { } owner
            ? context.Characters.Where(c => c.Id == owner).Select(c => c.Name).SingleOrDefault()
            : null;

        return new WorldInfo(world.Id, world.Kind, world.Template, world.TemplateVersion, (uint)world.Seed,
            world.OwnerCharacterId, ownerName, world.VisitAccess, world.BuildAccess);
    }

    public static List<(int X, int Y, int Z, byte Block)> LoadVoxelEdits(Guid worldId)
    {
        using var context = GameDatabase.Context();

        return [.. context.VoxelEdits.AsNoTracking()
            .Where(e => e.WorldId == worldId)
            .Select(e => new { e.X, e.Y, e.Z, e.Block })
            .AsEnumerable()
            .Select(e => (e.X, e.Y, e.Z, (byte)e.Block))];
    }

    public static void SaveVoxelEdit(Guid worldId, int x, int y, int z, byte block)
    {
        GameDatabase.Enqueue(context => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO voxel_edits (world_id, x, y, z, block) VALUES ({worldId}, {x}, {y}, {z}, {(short)block})
            ON CONFLICT (world_id, x, y, z) DO UPDATE SET block = excluded.block
            """));
    }

    public static List<PlacedDevice> LoadDevices(Guid worldId)
    {
        using var context = GameDatabase.Context();

        return [.. context.PlacedDevices.AsNoTracking().Where(d => d.WorldId == worldId).OrderBy(d => d.Id)];
    }

    public static void SaveDevices(Guid worldId, IEnumerable<PlacedDevice> devices)
    {
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

        GameDatabase.Enqueue(async context =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.PlacedDevices.Where(d => d.WorldId == worldId).ExecuteDeleteAsync();
            context.PlacedDevices.AddRange(snapshot);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    public static Dictionary<Guid, float[]> LoadCheckpoints(Guid worldId)
    {
        using var context = GameDatabase.Context();

        return context.Checkpoints.AsNoTracking()
            .Where(c => c.WorldId == worldId)
            .ToDictionary(c => c.CharacterId, c => new[] { c.X, c.Y, c.Z });
    }

    public static void SaveCheckpoint(Guid worldId, Guid characterId, float x, float y, float z)
    {
        GameDatabase.Enqueue(context => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO checkpoints (world_id, character_id, x, y, z) VALUES ({worldId}, {characterId}, {x}, {y}, {z})
            ON CONFLICT (world_id, character_id) DO UPDATE SET x = excluded.x, y = excluded.y, z = excluded.z
            """));
    }
}
