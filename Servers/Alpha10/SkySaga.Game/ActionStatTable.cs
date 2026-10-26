using System.Globalization;

namespace SkySaga.Game;

public readonly record struct ActionStats(string Name, float ChargeBarBoost, float WeaponLevelModifier, bool ConsumesChargeBar, string[] Tags, float Duration, float CreatureLevelModifier)
{
    public bool HasTag(string tag) => Tags?.Contains(tag, StringComparer.OrdinalIgnoreCase) == true;
}

public static class ActionStatTable
{
    private sealed record Data(Dictionary<string, ActionStats> ByName, Dictionary<uint, ActionStats> ByCrc);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data(new(StringComparer.OrdinalIgnoreCase), []);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionStats.txt");

        if (!File.Exists(path))
            return data;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 7 || parts[0][0] == '#')
                continue;

            var stats = new ActionStats(
                parts[0],
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                parts[3] == "1",
                parts[4] == "-" ? [] : parts[4].Split(','),
                float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture));

            data.ByName[parts[0]] = stats;
            data.ByCrc[Util.ComputeCrc32(parts[0])] = stats;
        }

        return data;
    }

    public static ActionStats Get(string equippedAction) => Table.Value.ByName.GetValueOrDefault(equippedAction);

    public static bool TryGet(uint crc, out ActionStats stats) => Table.Value.ByCrc.TryGetValue(crc, out stats);
}
