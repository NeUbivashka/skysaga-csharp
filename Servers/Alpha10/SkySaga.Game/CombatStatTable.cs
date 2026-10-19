namespace SkySaga.Game;

public readonly record struct CombatStats(float Health, float WeaponLevel, float ArmourLevel);

public static class CombatStatTable
{
    private static readonly Lazy<Dictionary<(string Kind, uint Crc), CombatStats>> StatsByCrc = new(Load);

    private static Dictionary<(string Kind, uint Crc), CombatStats> Load()
    {
        var map = new Dictionary<(string Kind, uint Crc), CombatStats>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CombatStats.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 5
                && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var health)
                && float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var weaponLevel)
                && float.TryParse(parts[4], System.Globalization.CultureInfo.InvariantCulture, out var armourLevel))
                map.TryAdd((parts[0], Util.ComputeCrc32(parts[1])), new CombatStats(health, weaponLevel, armourLevel));
        }

        return map;
    }

    public static bool TryGetEntity(string name, out CombatStats stats) => StatsByCrc.Value.TryGetValue(("Entity", Util.ComputeCrc32(name)), out stats);

    public static bool TryGetItem(uint itemCrc, out CombatStats stats) => StatsByCrc.Value.TryGetValue(("Item", itemCrc), out stats);
}
