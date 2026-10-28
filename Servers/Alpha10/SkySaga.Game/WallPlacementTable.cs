namespace SkySaga.Game;

public static class WallPlacementTable
{
    private static readonly Lazy<Dictionary<(string Entity, string Rule), (float Pitch, float Yaw)>> Rules = new(Load);

    private static Dictionary<(string Entity, string Rule), (float Pitch, float Yaw)> Load()
    {
        var map = new Dictionary<(string Entity, string Rule), (float Pitch, float Yaw)>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "WallPlacements.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 4 && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var pitch)
                && float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var yaw))
                map[(parts[0].ToLowerInvariant(), parts[1])] = (pitch, yaw);
        }

        return map;
    }

    public static (float Pitch, float Yaw) ForFace(string entity, byte face)
    {
        var rule = face switch
        {
            0 or 1 or 4 or 5 => "PlaceAgainst",
            3 => "PlaceBelow",
            _ => "PlaceAbove"
        };

        return Rules.Value.GetValueOrDefault((entity.ToLowerInvariant(), rule));
    }
}
