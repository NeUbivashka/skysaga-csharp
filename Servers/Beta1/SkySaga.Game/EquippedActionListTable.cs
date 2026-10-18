namespace SkySaga.Game;

public readonly record struct EquippedActionList(string EquippedAction, int Slot);

public static class EquippedActionListTable
{
    public const int AreaOfEffectSlot = 0;

    private static readonly Lazy<Dictionary<uint, EquippedActionList>> ListsById = new(Load);

    private static Dictionary<uint, EquippedActionList> Load()
    {
        var map = new Dictionary<uint, EquippedActionList>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "EquippedActionLists.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3 && uint.TryParse(parts[0], out var listId) && int.TryParse(parts[2], out var slot))
                map[listId] = new EquippedActionList(parts[1], slot);
        }

        return map;
    }

    public static bool TryGet(uint listId, out EquippedActionList list) => ListsById.Value.TryGetValue(listId, out list);

    public static uint? AreaOfEffectListOf(uint listId)
        => TryGet(listId, out var list) ? listId - (uint)(list.Slot - AreaOfEffectSlot) : null;
}
