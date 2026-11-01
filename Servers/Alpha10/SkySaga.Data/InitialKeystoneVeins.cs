using System.Text.Json;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialKeystoneVeins
{
    public const byte Block = 2;

    private const int ChunkSize = 32;
    private const int VeinLength = 3;
    private const int MinRadius = 6;
    private const int MaxRadius = 32;
    private const long ForestCrc = 2517439084;
    private const long DesertCrc = 2111478763;
    private const long JungleCrc = 3882895874;
    private const long MountainCrc = 433052062;

    private static readonly long[] Materials = [ForestCrc, DesertCrc, JungleCrc, MountainCrc];

    public static bool AddToNewIsland(SkySagaDbContext db, World island, byte[] template)
    {
        using var json = JsonDocument.Parse(template);
        var root = json.RootElement;
        if (!root.TryGetProperty("voxels", out var voxelArray)
            || !root.TryGetProperty("entities", out var entityArray)
            || !root.TryGetProperty("mapSizeChunks", out var size))
            return false;

        var sizeX = size.GetProperty("x").GetInt32() * ChunkSize;
        var sizeY = size.GetProperty("y").GetInt32() * ChunkSize;
        var sizeZ = size.GetProperty("z").GetInt32() * ChunkSize;

        var voxels = new HashSet<(int X, int Y, int Z)>();
        foreach (var voxel in voxelArray.EnumerateArray())
            if (voxel.GetProperty("block").GetInt32() != 255)
                voxels.Add((voxel.GetProperty("x").GetInt32(), voxel.GetProperty("y").GetInt32(), voxel.GetProperty("z").GetInt32()));

        var entities = new List<(int X, int Y, int Z)>();
        (int X, int Y, int Z)? spawn = null;
        foreach (var entity in entityArray.EnumerateArray())
        {
            var pos = (entity.GetProperty("x").GetInt32(), entity.GetProperty("y").GetInt32(), entity.GetProperty("z").GetInt32());
            entities.Add(pos);
            if (entity.GetProperty("name").GetString() == "Player") spawn = pos;
        }
        if (spawn is null) return false;

        var center = spawn.Value;
        var wanted = Materials.Length * VeinLength;
        var selected = new List<(int X, int Y, int Z)>();
        for (int radius = MinRadius; radius <= MaxRadius && selected.Count < wanted; radius++)
        for (int dz = -radius; dz <= radius && selected.Count < wanted; dz++)
        for (int dx = -radius; dx <= radius && selected.Count < wanted; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
            int x = center.X + dx, y = center.Y, z = center.Z + dz;
            if (x < 1 || x + VeinLength - 1 >= sizeX || y < 1 || y + 1 >= sizeY || z < 1 || z >= sizeZ) continue;
            bool valid = true;
            for (int i = 0; i < VeinLength; i++)
            {
                var pos = (X: x + i, Y: y, Z: z);
                if (voxels.Contains(pos) || voxels.Contains((pos.X, y + 1, z)) || !voxels.Contains((pos.X, y - 1, z))
                    || entities.Any(e => Math.Abs(e.X - pos.X) <= 2 && Math.Abs(e.Y - y) <= 2 && Math.Abs(e.Z - z) <= 2)
                    || selected.Any(p => Math.Abs(p.X - pos.X) <= 1 && p.Z == z)) { valid = false; break; }
            }
            if (!valid) continue;
            for (int i = 0; i < VeinLength; i++) selected.Add((x + i, y, z));
        }
        if (selected.Count != wanted) return false;

        for (int i = 0; i < selected.Count; i++)
        {
            var pos = selected[i];
            db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Block = Block });
            db.KeystoneVeins.Add(new KeystoneVein { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Material = Materials[i / VeinLength] });
        }
        return true;
    }
}
