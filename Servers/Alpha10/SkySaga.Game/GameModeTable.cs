namespace SkySaga.Game;

public static class GameModeTable
{
    private const double DefaultRespawnDelay = 10;

    private static readonly Lazy<Dictionary<string, (bool PlayersCanDamagePlayers, double RespawnDelay)>> Modes = new(Load);

    private static Dictionary<string, (bool PlayersCanDamagePlayers, double RespawnDelay)> Load()
    {
        var map = new Dictionary<string, (bool PlayersCanDamagePlayers, double RespawnDelay)>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "GameModes.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3)
                map[parts[0]] = (parts[1] == "1", double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        return map;
    }

    public static bool PlayersCanDamagePlayers(GameMode mode)
        => Modes.Value.GetValueOrDefault(PropertiesName(mode)).PlayersCanDamagePlayers;

    public static double RespawnDelay(GameMode mode)
        => Modes.Value.TryGetValue(PropertiesName(mode), out var properties) ? properties.RespawnDelay : DefaultRespawnDelay;

    private static string PropertiesName(GameMode mode)
        => "GameMode_" + (mode == GameMode.Social ? "SocialHub" : mode.ToString());
}
