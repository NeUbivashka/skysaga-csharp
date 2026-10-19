using System.Globalization;

namespace SkySaga.Game;

public readonly record struct ListEat(string EquippedAction, string EatAction, float HealthBoost);

public static class ActionListEatTable
{
    private static readonly Lazy<Dictionary<uint, ListEat>> EatsByList = new(Load);

    private static Dictionary<uint, ListEat> Load()
    {
        var map = new Dictionary<uint, ListEat>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionListEat.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 4 || !uint.TryParse(parts[0], out var listId))
                continue;

            map[listId] = new ListEat(parts[1], parts[2], float.Parse(parts[3], CultureInfo.InvariantCulture));
        }

        return map;
    }

    public static bool TryGet(uint listId, out ListEat eat) => EatsByList.Value.TryGetValue(listId, out eat);
}
