using System.Text;
using System.Text.Json;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialOreVeins
{
    public const byte Block = 2;

    private const int ChunkSize = 32;
    private const int VeinLength = 3;
    private const int VeinGap = 4;
    private const int MinRadius = 8;
    private const int MaxRadius = 48;

    private static readonly (OreVeinKind Kind, string Material)[] Veins =
    [
        (OreVeinKind.Keystone, "Forest"),
        (OreVeinKind.Keystone, "Desert"),
        (OreVeinKind.Keystone, "Jungle"),
        (OreVeinKind.Keystone, "Mountain"),
        (OreVeinKind.Metal, "Metal_Black"),
        (OreVeinKind.Metal, "Metal_Blue"),
        (OreVeinKind.Metal, "Metal_Brown"),
        (OreVeinKind.Metal, "Metal_Cyan"),
        (OreVeinKind.Metal, "Metal_Green"),
        (OreVeinKind.Metal, "Metal_Grey"),
        (OreVeinKind.Metal, "Metal_Lime"),
        (OreVeinKind.Metal, "Metal_Orange"),
        (OreVeinKind.Metal, "Metal_Pink"),
        (OreVeinKind.Metal, "Metal_Purple"),
        (OreVeinKind.Metal, "Metal_Red"),
        (OreVeinKind.Metal, "Metal_White"),
        (OreVeinKind.Metal, "Metal_Yellow")
    ];

    public static bool AddToIsland(SkySagaDbContext db, World island, byte[] template, bool addCaveDisplay = false)
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

        var edits = db.VoxelEdits.Where(edit => edit.WorldId == island.Id).ToDictionary(edit => (edit.X, edit.Y, edit.Z));
        foreach (var edit in edits)
            if (edit.Value.Block == 255) voxels.Remove(edit.Key);
            else voxels.Add(edit.Key);

        var entities = new List<(int X, int Y, int Z)>();
        (int X, int Y, int Z)? spawn = null;
        foreach (var entity in entityArray.EnumerateArray())
        {
            var pos = (entity.GetProperty("x").GetInt32(), entity.GetProperty("y").GetInt32(), entity.GetProperty("z").GetInt32());
            entities.Add(pos);
            if (entity.GetProperty("name").GetString() == "Player") spawn = pos;
        }
        if (spawn is null) return false;
        entities.AddRange(db.PlacedDevices.Where(device => device.WorldId == island.Id).Select(device => new ValueTuple<int, int, int>(device.X, device.Y, device.Z)));

        var center = spawn.Value;
        var wanted = Veins.Length * VeinLength;
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
                    || selected.Any(p => Math.Abs(p.X - pos.X) <= VeinGap && Math.Abs(p.Z - z) <= VeinGap)) { valid = false; break; }
            }
            if (!valid) continue;
            for (int i = 0; i < VeinLength; i++) selected.Add((x + i, y, z));
        }
        if (selected.Count != wanted) return false;

        for (int i = 0; i < selected.Count; i++)
        {
            var pos = selected[i];
            var vein = Veins[i / VeinLength];
            if (edits.TryGetValue(pos, out var edit)) edit.Block = Block;
            else db.VoxelEdits.Add(new VoxelEdit { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Block = Block });
            db.OreVeins.Add(new OreVein { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Kind = vein.Kind, Material = NameCrc(vein.Material) });
        }
        if (!addCaveDisplay) return true;

        var crystals = new List<(int X, int Y, int Z)>();
        foreach (var vein in selected.Take(4 * VeinLength).Chunk(VeinLength))
        {
            var origin = vein[1];
            var found = false;
            for (var radius = 1; radius <= 4 && !found; radius++)
            for (var dz = -radius; dz <= radius && !found; dz++)
            for (var dx = -radius; dx <= radius && !found; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                var pos = (X: origin.X + dx, Y: origin.Y, Z: origin.Z + dz);
                if (pos.X < 1 || pos.X >= sizeX || pos.Z < 1 || pos.Z >= sizeZ
                    || voxels.Contains(pos) || voxels.Contains((pos.X, pos.Y + 1, pos.Z))
                    || !voxels.Contains((pos.X, pos.Y - 1, pos.Z))
                    || entities.Any(e => Math.Abs(e.X - pos.X) <= 2 && Math.Abs(e.Y - pos.Y) <= 2 && Math.Abs(e.Z - pos.Z) <= 2)
                    || selected.Contains(pos) || crystals.Contains(pos)) continue;
                crystals.Add(pos);
                found = true;
            }
        }
        long[] crystalMaterials = [2315324841, 228787774, 2697292451, 1536416095, 3667810571];
        for (var i = 0; i < crystals.Count; i++)
        {
            var pos = crystals[i];
            db.PlacedDevices.Add(new PlacedDevice { WorldId = island.Id, ItemCrc = 1422256949, X = pos.X, Y = pos.Y, Z = pos.Z,
                Materials = [crystalMaterials[i]], Face = 2 });
        }
        if (!InitialCaveDisplay.Add(db, island, voxels, entities, selected, crystals, center))
            Console.WriteLine($"[WorldCatalog] No safe space found for cave display on new island {island.Id}.");
        return true;
    }

    private static long NameCrc(string name)
    {
        uint crc = 0;

        foreach (var value in Encoding.UTF8.GetBytes(name.ToLowerInvariant()))
        {
            crc ^= (uint)value << 24;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
        }

        return crc;
    }
}
