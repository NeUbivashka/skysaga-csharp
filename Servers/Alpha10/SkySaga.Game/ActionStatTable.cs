using System.Globalization;

namespace SkySaga.Game;

public readonly record struct ActionStats(float ChargeBarBoost, float WeaponLevelModifier, bool ConsumesChargeBar);

public static class ActionStatTable
{
    private static readonly Lazy<Dictionary<string, ActionStats>> ByName = new(Load);

    private static Dictionary<string, ActionStats> Load()
    {
        var map = new Dictionary<string, ActionStats>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionStats.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 4 && parts[0][0] != '#')
                map[parts[0]] = new ActionStats(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    parts[3] == "1");
        }

        return map;
    }

    public static ActionStats Get(string equippedAction) => ByName.Value.GetValueOrDefault(equippedAction);
}
