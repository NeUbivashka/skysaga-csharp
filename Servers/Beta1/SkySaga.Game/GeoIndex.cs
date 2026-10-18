using System.Linq;

namespace SkySaga.Game;

public static class GeoIndex
{
    private static readonly Lazy<Dictionary<string, Dictionary<string, uint>>> Tables = new(Load);

    private static Dictionary<string, Dictionary<string, uint>> Load()
    {
        var tables = new Dictionary<string, Dictionary<string, uint>>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "GeoIndex.txt");

        if (!File.Exists(path))
        {
            Console.WriteLine($"[GeoIndex] {path} missing.");
            return tables;
        }

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ');

            if (parts.Length != 3 || line[0] == '#' || !uint.TryParse(parts[1], out var index))
                continue;

            if (!tables.TryGetValue(parts[0], out var table))
                tables[parts[0]] = table = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            table[parts[2]] = index;
        }

        return tables;
    }

    public static uint Count(string table)
        => (uint)(Tables.Value.TryGetValue(table, out var entries) ? entries.Count : 0) + 1;

    public static uint WireIndex(string table, string name)
    {
        if (Tables.Value.TryGetValue(table, out var entries) && entries.TryGetValue(name, out var index))
            return index + 1;

        Console.WriteLine($"[GeoIndex] {table} has no entry {name}.");
        return 0;
    }

    public static IEnumerable<(string Name, uint WireIndex)> Entries(string table)
        => Tables.Value.TryGetValue(table, out var entries)
            ? entries.Select(entry => (entry.Key, entry.Value + 1))
            : [];
}
