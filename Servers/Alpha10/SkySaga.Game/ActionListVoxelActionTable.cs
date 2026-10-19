namespace SkySaga.Game;

public static class ActionListVoxelActionTable
{
    private static readonly Lazy<Dictionary<uint, HashSet<string>>> CategoriesByList = new(Load);

    private static Dictionary<uint, HashSet<string>> Load()
    {
        var map = new Dictionary<uint, HashSet<string>>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionListVoxelActions.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 5 || !uint.TryParse(parts[0], out var listId))
                continue;

            if (!map.TryGetValue(listId, out var categories))
                map[listId] = categories = [];

            categories.Add(parts[3]);
        }

        return map;
    }

    public static bool Has(uint listId, string category)
        => CategoriesByList.Value.TryGetValue(listId, out var categories) && categories.Contains(category);
}
