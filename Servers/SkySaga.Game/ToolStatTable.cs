namespace SkySaga.Game;

public readonly record struct ToolStats(float VoxelStrength, float MiningPowerTier);

public static class ToolStatTable
{
    private static readonly Lazy<Dictionary<uint, ToolStats>> StatsByCrc = new(Load);

    private static Dictionary<uint, ToolStats> Load()
    {
        var map = new Dictionary<uint, ToolStats>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ToolStats.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3
                && float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var strength)
                && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var tier))
                map.TryAdd(Util.ComputeCrc32(parts[0]), new ToolStats(strength, tier));
        }

        return map;
    }

    public static bool TryGet(uint nameCrc, out ToolStats stats) => StatsByCrc.Value.TryGetValue(nameCrc, out stats);
}
