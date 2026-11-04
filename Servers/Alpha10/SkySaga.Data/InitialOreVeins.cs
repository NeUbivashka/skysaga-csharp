using System.Text;
using System.Text.Json;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class InitialOreVeins
{
    public const byte Block = 56;

    private static readonly HashSet<int> RockBlocks = [3, 16, 22, 27, 31, 46, 49, 51, 52, 53, 54, 55, 56];

    private const int ChunkSize = 32;
    public const int VeinLength = 3;

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

    internal sealed record IslandSite(HashSet<(int X, int Y, int Z)> Voxels, List<(int X, int Y, int Z)> Entities, Dictionary<(int X, int Y, int Z), VoxelEdit> Edits,
        (int X, int Y, int Z) Spawn, (int X, int Y, int Z) Arrival, (int X, int Y, int Z) Size, HashSet<(int X, int Y, int Z)> Rock);

    public static bool IsRock(int block) => RockBlocks.Contains(block);

    private static bool IsExposed(HashSet<(int X, int Y, int Z)> voxels, (int X, int Y, int Z) cell)
        => !voxels.Contains((cell.X, cell.Y, cell.Z - 1)) || !voxels.Contains((cell.X, cell.Y, cell.Z + 1));

    internal static IslandSite? ReadSite(SkySagaDbContext db, World island, byte[] template)
    {
        using var json = JsonDocument.Parse(template);
        var root = json.RootElement;
        if (!root.TryGetProperty("voxels", out var voxelArray)
            || !root.TryGetProperty("entities", out var entityArray)
            || !root.TryGetProperty("mapSizeChunks", out var size))
            return null;

        var sizeX = size.GetProperty("x").GetInt32() * ChunkSize;
        var sizeY = size.GetProperty("y").GetInt32() * ChunkSize;
        var sizeZ = size.GetProperty("z").GetInt32() * ChunkSize;

        var voxels = new HashSet<(int X, int Y, int Z)>();
        var rock = new HashSet<(int X, int Y, int Z)>();
        foreach (var voxel in voxelArray.EnumerateArray())
        {
            var block = voxel.GetProperty("block").GetInt32();
            if (block == 255)
                continue;

            var cell = (voxel.GetProperty("x").GetInt32(), voxel.GetProperty("y").GetInt32(), voxel.GetProperty("z").GetInt32());
            voxels.Add(cell);
            if (IsRock(block))
                rock.Add(cell);
        }

        var edits = db.VoxelEdits.Where(edit => edit.WorldId == island.Id).ToDictionary(edit => (edit.X, edit.Y, edit.Z));
        foreach (var edit in edits)
        {
            if (edit.Value.Block == 255) voxels.Remove(edit.Key);
            else voxels.Add(edit.Key);
            if (IsRock(edit.Value.Block)) rock.Add(edit.Key);
            else rock.Remove(edit.Key);
        }

        var entities = new List<(int X, int Y, int Z)>();
        (int X, int Y, int Z)? spawn = null;
        (int X, int Y, int Z)? airship = null;
        foreach (var entity in entityArray.EnumerateArray())
        {
            if (entity.TryGetProperty("onlyIn", out var onlyIn) && onlyIn.GetString() != island.Kind.ToString())
                continue;

            var pos = (entity.GetProperty("x").GetInt32(), entity.GetProperty("y").GetInt32(), entity.GetProperty("z").GetInt32());
            entities.Add(pos);
            if (entity.GetProperty("name").GetString() == "Player") spawn = pos;
            if (entity.GetProperty("name").GetString() == "Airship") airship = pos;
        }
        if (spawn is null) return null;
        entities.AddRange(db.PlacedDevices.Where(device => device.WorldId == island.Id).Select(device => new ValueTuple<int, int, int>(device.X, device.Y, device.Z)));

        return new IslandSite(voxels, entities, edits, spawn.Value, airship ?? spawn.Value, (sizeX, sizeY, sizeZ), rock);
    }

    public static bool AddToIsland(SkySagaDbContext db, World island, byte[] template, bool addCaveDisplay = false)
    {
        if (ReadSite(db, island, template) is not { } site)
            return false;

        var wanted = Veins.Length * VeinLength;
        var selected = new List<(int X, int Y, int Z)>();
        foreach (var start in site.Rock.OrderBy(cell => cell.Y).ThenBy(cell => cell.Z).ThenBy(cell => cell.X))
        {
            if (selected.Count == wanted) break;
            var run = Enumerable.Range(0, VeinLength).Select(i => (X: start.X + i, start.Y, start.Z)).ToList();
            if (run.All(cell => site.Rock.Contains(cell) && !selected.Contains(cell) && !site.Entities.Contains(cell) && IsExposed(site.Voxels, cell)))
                selected.AddRange(run);
        }
        if (selected.Count != wanted) return false;

        for (int i = 0; i < selected.Count; i++)
        {
            var pos = selected[i];
            var vein = Veins[i / VeinLength];
            db.OreVeins.Add(new OreVein { WorldId = island.Id, X = pos.X, Y = pos.Y, Z = pos.Z, Kind = vein.Kind, Material = NameCrc(vein.Material) });
        }
        if (addCaveDisplay)
            InitialCaveDisplay.Add(db, island, site, selected, VeinLength);

        return true;
    }

    internal static long NameCrc(string name)
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
