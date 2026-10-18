namespace SkySaga.Game;

public static class ItemTable
{
    public const uint Count = 1810;

    private static readonly Lazy<Dictionary<uint, ushort>> ByCrc = new(Load);
    private static readonly List<string> Names = [""];

    private static Dictionary<uint, ushort> Load()
    {
        var map = new Dictionary<uint, ushort>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Resources.txt");

        if (!File.Exists(path))
        {
            Console.WriteLine($"[ItemTable] {path} missing - every item will read as none.");
            return map;
        }

        ushort index = 0;

        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            map.TryAdd(Util.ComputeCrc32(line), ++index);
            Names.Add(line);
        }

        return map;
    }

    public static ushort IndexOf(uint nameCrc) => ByCrc.Value.GetValueOrDefault(nameCrc);

    public static ushort IndexOf(string name) => IndexOf(Util.ComputeCrc32(name));

    public static string NameOf(ushort index)
    {
        _ = ByCrc.Value;
        return index < Names.Count ? Names[index] : "";
    }
}
