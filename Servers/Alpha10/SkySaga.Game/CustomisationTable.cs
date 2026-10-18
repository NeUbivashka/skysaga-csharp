namespace SkySaga.Game;

public static class CustomisationTable
{
    private static readonly Lazy<Dictionary<string, byte[]>> DataByList = new(Load);

    private static Dictionary<string, byte[]> Load()
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Customisations.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
                map.TryAdd(parts[0], Convert.FromBase64String(parts[1]));
        }

        return map;
    }

    public static bool TryGet(string listName, out byte[] data) => DataByList.Value.TryGetValue(listName, out data!);
}
