namespace SkySaga.Game;

public static class BlockCrcMapping
{
    private static readonly Lazy<(Dictionary<int, uint> ItemByVoxel, Dictionary<uint, int> VoxelByItem)> Tables = new(Load);

    private static (Dictionary<int, uint>, Dictionary<uint, int>) Load()
    {
        var itemByVoxel = new Dictionary<int, uint>();
        var voxelByItem = new Dictionary<uint, int>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "VoxelLoot.txt");

        if (!File.Exists(path))
            return (itemByVoxel, voxelByItem);

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3 || !int.TryParse(parts[0], out var voxel))
                continue;

            var itemCrc = Util.ComputeCrc32(parts[1]);

            itemByVoxel[voxel] = itemCrc;

            if (parts[1] == parts[2])
                voxelByItem[itemCrc] = voxel;
            else
                voxelByItem.TryAdd(itemCrc, voxel);
        }

        return (itemByVoxel, voxelByItem);
    }

    public static uint? GetItemCrc(int blockId)
        => Tables.Value.ItemByVoxel.TryGetValue(blockId, out var crc) ? crc : null;

    public static int? GetBlockId(uint crc)
        => Tables.Value.VoxelByItem.TryGetValue(crc, out var blockId) ? blockId : null;
}
