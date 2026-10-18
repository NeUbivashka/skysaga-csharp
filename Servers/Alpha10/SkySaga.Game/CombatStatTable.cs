namespace SkySaga.Game;

public readonly record struct CombatStats(float Health, float WeaponLevel, float ArmourLevel);

public static class CombatStatTable
{
    private static readonly Lazy<Dictionary<uint, CombatStats>> StatsByCrc = new(Load);

    private static Dictionary<uint, CombatStats> Load()
    {
        var map = new Dictionary<uint, CombatStats>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CombatStats.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 4
                && float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var health)
                && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var weaponLevel)
                && float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var armourLevel))
                map.TryAdd(Util.ComputeCrc32(parts[0]), new CombatStats(health, weaponLevel, armourLevel));
        }

        return map;
    }

    public static bool TryGet(uint nameCrc, out CombatStats stats) => StatsByCrc.Value.TryGetValue(nameCrc, out stats);

    public static bool TryGet(string name, out CombatStats stats) => TryGet(Util.ComputeCrc32(name), out stats);
}
