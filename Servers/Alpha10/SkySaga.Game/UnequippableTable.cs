using System.Linq;

namespace SkySaga.Game;

public static class UnequippableTable
{
    private static readonly Lazy<HashSet<uint>> Items = new(Load);

    private static HashSet<uint> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Unequippable.txt");

        return File.Exists(path) ? [.. File.ReadLines(path).Where(line => line.Length > 0).Select(name => Util.ComputeCrc32(name))] : [];
    }

    public static bool Contains(uint itemCrc) => Items.Value.Contains(itemCrc);
}
