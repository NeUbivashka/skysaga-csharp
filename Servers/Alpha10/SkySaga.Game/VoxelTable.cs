namespace SkySaga.Game;

public readonly record struct VoxelStats(int Durability, int Resistance, int MiningToughnessTier, bool IsDiggable);

public static class VoxelTable
{
    private static readonly Lazy<Dictionary<byte, VoxelStats>> StatsByIndex = new(Load);

    private static Dictionary<byte, VoxelStats> Load()
    {
        var map = new Dictionary<byte, VoxelStats>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "VoxelStats.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 5
                && byte.TryParse(parts[0], out var index)
                && int.TryParse(parts[1], out var durability)
                && int.TryParse(parts[2], out var resistance)
                && int.TryParse(parts[3], out var toughness))
                map.TryAdd(index, new VoxelStats(durability, resistance, toughness, parts[4] == "1"));
        }

        return map;
    }

    public static bool TryGet(byte voxelIndex, out VoxelStats stats) => StatsByIndex.Value.TryGetValue(voxelIndex, out stats);
}
