using System.Linq;

namespace SkySaga.Game;

public static class MaterialTable
{
    private static readonly Lazy<Dictionary<uint, uint?[]>> ByCrc = new(Load);

    private static Dictionary<uint, uint?[]> Load()
    {
        var map = new Dictionary<uint, uint?[]>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Materials.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 5)
                continue;

            map.TryAdd(Util.ComputeCrc32(parts[0]),
                [.. parts.Skip(1).Where(m => m != "-").Select(m => (uint?)Util.ComputeCrc32(m))]);
        }

        return map;
    }

    public static bool TryGet(uint nameCrc, out uint?[] materials) => ByCrc.Value.TryGetValue(nameCrc, out materials!);
}
