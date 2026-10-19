using System.Linq;

namespace SkySaga.Game;

public readonly record struct CombatStats(float Health, float WeaponLevel, float ArmourLevel, float KnockbackDistance, float KnockbackStrength, float KnockbackResistance);

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

            if (parts.Length != 8)
                continue;

            var values = new float[6];

            if (Enumerable.Range(0, 6).All(i => float.TryParse(parts[i + 2], System.Globalization.CultureInfo.InvariantCulture, out values[i])))
                map.TryAdd((parts[0], Util.ComputeCrc32(parts[1])), new CombatStats(values[0], values[1], values[2], values[3], values[4], values[5]));
        }

        return map;
    }

    public static bool TryGetEntity(string name, out CombatStats stats) => StatsByCrc.Value.TryGetValue(("Entity", Util.ComputeCrc32(name)), out stats);

    public static bool TryGetItem(uint itemCrc, out CombatStats stats) => StatsByCrc.Value.TryGetValue(("Item", itemCrc), out stats);
}
