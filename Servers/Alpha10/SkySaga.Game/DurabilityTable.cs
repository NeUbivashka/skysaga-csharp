namespace SkySaga.Game;

public static class DurabilityTable
{
    private static readonly Lazy<Dictionary<uint, uint>> MaxByCrc = new(Load);

    private static Dictionary<uint, uint> Load()
    {
        var map = new Dictionary<uint, uint>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Durability.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2 && uint.TryParse(parts[1], out var max))
                map.TryAdd(Util.ComputeCrc32(parts[0]), max);
        }

        return map;
    }

    public static bool TryGetMax(uint nameCrc, out uint max) => MaxByCrc.Value.TryGetValue(nameCrc, out max);
}
