namespace SkySaga.Game;

public readonly record struct ListVoxelAction(string EquippedAction, string VoxelAction, string Category, bool MustBeOnFloor);

public static class ActionListVoxelActionTable
{
    public const string Dig = "DigAction";
    public const string PlaceVoxel = "PlaceVoxelAction";

    private static readonly Lazy<Dictionary<uint, List<ListVoxelAction>>> ActionsByList = new(Load);

    private static Dictionary<uint, List<ListVoxelAction>> Load()
    {
        var map = new Dictionary<uint, List<ListVoxelAction>>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionListVoxelActions.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 5 || !uint.TryParse(parts[0], out var listId))
                continue;

            if (!map.TryGetValue(listId, out var actions))
                map[listId] = actions = [];

            actions.Add(new ListVoxelAction(parts[1], parts[2], parts[3], parts[4] == "1"));
        }

        return map;
    }

    public static bool Has(uint listId, string category)
        => ActionsByList.Value.TryGetValue(listId, out var actions) && actions.Exists(action => action.Category == category);
}
