namespace SkySaga.Game;

public static class TagNameTable
{
    private static readonly Lazy<Dictionary<uint, uint>> ByTagCrc = new(Load);

    private static Dictionary<uint, uint> Load()
    {
        var map = new Dictionary<uint, uint>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "TagNames.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
                map.TryAdd(Util.ComputeCrc32(parts[0]), Util.ComputeCrc32(parts[1]));
        }

        return map;
    }

    public static uint? NameOf(IEnumerable<uint> tagCrcs)
    {
        foreach (var tag in tagCrcs)
            if (ByTagCrc.Value.TryGetValue(tag, out var name))
                return name;

        return null;
    }
}
