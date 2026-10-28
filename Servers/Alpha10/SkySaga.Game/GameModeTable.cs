namespace SkySaga.Game;

public static class GameModeTable
{
    private static readonly Lazy<Dictionary<string, bool>> PlayersCanDamagePlayersByMode = new(Load);

    private static Dictionary<string, bool> Load()
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "GameModes.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
                map[parts[0]] = parts[1] == "1";
        }

        return map;
    }

    public static bool PlayersCanDamagePlayers(GameMode mode)
        => PlayersCanDamagePlayersByMode.Value.GetValueOrDefault(PropertiesName(mode));

    private static string PropertiesName(GameMode mode)
        => "GameMode_" + (mode == GameMode.Social ? "SocialHub" : mode.ToString());
}
