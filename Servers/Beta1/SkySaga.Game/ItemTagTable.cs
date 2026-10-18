using System.Linq;

namespace SkySaga.Game;

public static class ItemTagTable
{
    private static readonly Lazy<Dictionary<uint, List<uint>>> ByCrc = new(Load);

    private static Dictionary<uint, List<uint>> Load()
    {
        var map = new Dictionary<uint, List<uint>>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ItemTags.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 1)
                map.TryAdd(Util.ComputeCrc32(parts[0]), [.. parts.Skip(1).Select(t => Util.ComputeCrc32(t))]);
        }

        return map;
    }

    public static bool TryGet(uint nameCrc, out List<uint> tags) => ByCrc.Value.TryGetValue(nameCrc, out tags!);
}
