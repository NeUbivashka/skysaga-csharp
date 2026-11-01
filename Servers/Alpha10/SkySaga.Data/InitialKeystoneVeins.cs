using System.Text.Json;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialKeystoneVeins
{
    public const long ItemCrc = 2835187850;
    private static readonly long[] Materials = [2517439084, 2111478763, 3882895874, 433052062];

    public static bool AddToNewIsland(SkySagaDbContext db, World island, byte[] template)
    {
        using var json = JsonDocument.Parse(template);
        var root = json.RootElement;
        if (!root.TryGetProperty("voxels", out var voxelArray) || !root.TryGetProperty("entities", out var entityArray))
            return false;

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
        var selected = new List<(int X, int Y, int Z)>();
        for (int radius = 6; radius <= 32 && selected.Count < 12; radius++)
        for (int dz = -radius; dz <= radius && selected.Count < 12; dz++)
        for (int dx = -radius; dx <= radius && selected.Count < 12; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
            int x = center.X + dx, y = center.Y, z = center.Z + dz;
            if (x < 1 || x + 2 >= 192 || y < 1 || y + 1 >= 128 || z < 1 || z >= 192) continue;
            bool valid = true;
            for (int i = 0; i < 3; i++)
            {
                var pos = (X: x + i, Y: y, Z: z);
                if (voxels.Contains(pos) || voxels.Contains((pos.X, y + 1, z)) || !voxels.Contains((pos.X, y - 1, z))
                    || entities.Any(e => Math.Abs(e.X - pos.X) <= 2 && Math.Abs(e.Y - y) <= 2 && Math.Abs(e.Z - z) <= 2)
                    || selected.Any(p => Math.Abs(p.X - pos.X) <= 1 && p.Z == z)) { valid = false; break; }
            }
            if (!valid) continue;
            for (int i = 0; i < 3; i++) selected.Add((x + i, y, z));
        }
        if (selected.Count != 12) return false;

        for (int i = 0; i < selected.Count; i++)
        {
            var pos = selected[i];
            db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Block = 2 });
            db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = ItemCrc, X = pos.X, Y = pos.Y, Z = pos.Z,
                Materials = [Materials[i / 3]], Face = (byte)(i % 3) });
        }
        return true;
    }
}
