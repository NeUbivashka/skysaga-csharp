using System.Linq;

namespace SkySaga.Game;

public static class TwoHandedTable
{
    private static readonly Lazy<HashSet<int>> Items = new(Load);

    private static HashSet<int> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "TwoHanded.txt");

        return File.Exists(path)
            ? [.. File.ReadLines(path).Where(line => line.Length > 0).Select(name => (int)ItemTable.IndexOf(Util.ComputeCrc32(name)))]
            : [];
    }

    public static bool Contains(int itemIndex) => itemIndex != 0 && Items.Value.Contains(itemIndex);
}
