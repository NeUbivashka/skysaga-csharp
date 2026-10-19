namespace SkySaga.Game;

public static class StackLimitTable
{
    private const int DefaultLimit = 999;

    private static readonly Lazy<Dictionary<uint, int>> LimitsByItem = new(Load);

    private static Dictionary<uint, int> Load()
    {
        var map = new Dictionary<uint, int>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "StackLimits.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2 && int.TryParse(parts[1], out var limit))
                map[Util.ComputeCrc32(parts[0])] = limit;
        }

        return map;
    }

    public static int Get(uint? itemCrc)
        => itemCrc is uint crc && LimitsByItem.Value.TryGetValue(crc, out var limit) ? limit : DefaultLimit;
}
