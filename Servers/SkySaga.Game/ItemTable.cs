namespace SkySaga.Game;

/// <summary>
/// The client's item table: wire index of an item (resource) by name or name CRC.
///
/// The client keeps GeoData's Resources in Index order behind a null entry 0
/// ([0x01487BCC]+0x1F28, count +0x1F2C = 1810; entry +0x8 is the name CRC, +0xC the index), and
/// every item reference on the wire is a ranged index into it (FUN_007D8110, 11 bits), not a CRC.
/// Data/Resources.txt holds the names in that order.
/// </summary>
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

    /// <summary>Wire index of the item whose lower-case name CRC is <paramref name="nameCrc"/>; 0 if unknown.</summary>
    public static ushort IndexOf(uint nameCrc) => ByCrc.Value.GetValueOrDefault(nameCrc);

    public static ushort IndexOf(string name) => IndexOf(Util.ComputeCrc32(name));

    /// <summary>Resource name at a wire index; empty for 0 or an unknown index.</summary>
    public static string NameOf(ushort index)
    {
        _ = ByCrc.Value;
        return index < Names.Count ? Names[index] : "";
    }
}
