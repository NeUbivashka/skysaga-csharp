using System.Linq;

namespace SkySaga.Game;

public static class OwnerOnlyTable
{
    private static readonly Lazy<HashSet<string>> Components = new(Load);

    public static bool Contains(string componentName) => Components.Value.Contains(componentName);

    private static HashSet<string> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "OwnerOnly.txt");

        return File.Exists(path)
            ? new HashSet<string>(File.ReadLines(path).Select(line => line.Trim()).Where(line => line.Length > 0), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
