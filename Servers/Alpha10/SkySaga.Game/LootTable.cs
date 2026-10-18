using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public readonly record struct LootDrop(uint ItemCrc, int Count, string?[] Materials);

public static class LootTable
{
    private const int MaxDepth = 8;

    private readonly record struct Biomes(bool Invert, string[] Names)
    {
        public bool Allows(string biome)
            => Names.Length == 0 || Names.Contains(biome, StringComparer.OrdinalIgnoreCase) != Invert;

        public static Biomes Parse(string text)
            => text == "-" ? new(false, []) : new(text.StartsWith('!'), text.TrimStart('!').Split(','));
    }

    private readonly record struct TableEntry(bool IsList, string Name, int Quantity, float Percentage, Biomes Biomes);

    private readonly record struct ListEntry(string Resource, int Quantity, float Frequency, Biomes Biomes, string?[] Materials);

    public const string EntityMaterial = "@entity";

    private sealed record LootList(bool AddAll, List<ListEntry> Entries);

    private static readonly Lazy<(Dictionary<string, List<TableEntry>> Tables, Dictionary<string, LootList> Lists, HashSet<string> Meats)> Data = new(Load);

    private static (Dictionary<string, List<TableEntry>>, Dictionary<string, LootList>, HashSet<string>) Load()
    {
        var tables = new Dictionary<string, List<TableEntry>>(StringComparer.OrdinalIgnoreCase);
        var lists = new Dictionary<string, LootList>(StringComparer.OrdinalIgnoreCase);
        var meats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "LootTables.txt");

        if (!File.Exists(path))
            return (tables, lists, meats);

        List<TableEntry>? table = null;
        LootList? list = null;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts)
            {
                case ["table", var name]:
                    tables[name] = table = [];
                    list = null;
                    break;
                case ["list", var name, var mode]:
                    lists[name] = list = new LootList(mode == "all", []);
                    table = null;
                    break;
                case [var kind and ("list" or "table"), var name, var quantity, var percentage, var biomes] when table is not null:
                    table.Add(new TableEntry(kind == "list", name, int.Parse(quantity), Parse(percentage), Biomes.Parse(biomes)));
                    break;
                case ["item", var resource, var quantity, var frequency, var biomes, .. var materials] when list is not null:
                    list.Entries.Add(new ListEntry(resource, int.Parse(quantity), Parse(frequency), Biomes.Parse(biomes),
                        [.. materials.Select(material => material == "-" ? null : material)]));
                    break;
                case ["meat", var material]:
                    meats.Add(material);
                    break;
            }
        }

        return (tables, lists, meats);
    }

    private static float Parse(string text) => float.Parse(text, CultureInfo.InvariantCulture);

    public static bool Exists(string tableName) => Data.Value.Tables.ContainsKey(tableName);

    public static bool IsMeat(string material) => Data.Value.Meats.Contains(material);

    public static bool IsMeat(uint materialCrc) => Data.Value.Meats.Any(material => Util.ComputeCrc32(material) == materialCrc);

    public static List<LootDrop> Roll(string tableName, string biome)
    {
        var drops = new List<LootDrop>();

        RollTable(tableName, biome, drops, 0);

        return drops;
    }

    private static void RollTable(string tableName, string biome, List<LootDrop> drops, int depth)
    {
        if (depth > MaxDepth || !Data.Value.Tables.TryGetValue(tableName, out var entries))
            return;

        foreach (var entry in entries)
        {
            if (!entry.Biomes.Allows(biome) || Random.Shared.NextSingle() * 100 >= entry.Percentage)
                continue;

            for (var i = 0; i < entry.Quantity; i++)
            {
                if (entry.IsList)
                    RollList(entry.Name, biome, drops);
                else
                    RollTable(entry.Name, biome, drops, depth + 1);
            }
        }
    }

    private static void RollList(string listName, string biome, List<LootDrop> drops)
    {
        if (!Data.Value.Lists.TryGetValue(listName, out var list))
            return;

        var valid = list.Entries.Where(entry => entry.Biomes.Allows(biome)).ToList();

        if (list.AddAll)
        {
            drops.AddRange(valid.Select(entry => new LootDrop(Util.ComputeCrc32(entry.Resource), entry.Quantity, entry.Materials)));
            return;
        }

        var total = valid.Sum(entry => entry.Frequency);
        var roll = Random.Shared.NextSingle() * total;

        foreach (var entry in valid)
        {
            roll -= entry.Frequency;

            if (roll < 0)
            {
                drops.Add(new LootDrop(Util.ComputeCrc32(entry.Resource), entry.Quantity, entry.Materials));
                return;
            }
        }
    }
}
