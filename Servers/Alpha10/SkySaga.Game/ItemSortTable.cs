namespace SkySaga.Game;

public readonly record struct ItemSortInfo(string Name, int ObjectType, int Rarity);

public static class ItemSortTable
{
    public const int NoObjectType = 16;

    private static readonly Lazy<Dictionary<uint, ItemSortInfo>> ByItem = new(Load);

    private static Dictionary<uint, ItemSortInfo> Load()
    {
        var map = new Dictionary<uint, ItemSortInfo>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ItemSort.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3 && !parts[0].StartsWith('#') && int.TryParse(parts[1], out var objectType) && int.TryParse(parts[2], out var rarity))
                map[Util.ComputeCrc32(parts[0])] = new ItemSortInfo(parts[0], objectType, rarity);
        }

        return map;
    }

    public static bool TryGet(uint itemCrc, out ItemSortInfo info) => ByItem.Value.TryGetValue(itemCrc, out info);
}
