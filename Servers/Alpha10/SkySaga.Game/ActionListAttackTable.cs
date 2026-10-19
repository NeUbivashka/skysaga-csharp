using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public readonly record struct ListAttack(string EquippedAction, string EntityAction, bool ForceZeroDamage, (float Input, float Output)[]? DamageLut,
    float KnockbackDistance, float KnockbackStrength, float KnockbackPitch, float KnockbackYaw);

public static class ActionListAttackTable
{
    private static readonly Lazy<Dictionary<uint, List<ListAttack>>> AttacksByList = new(Load);

    private static Dictionary<uint, List<ListAttack>> Load()
    {
        var map = new Dictionary<uint, List<ListAttack>>();
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "ActionListAttacks.txt");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 9 || !uint.TryParse(parts[0], out var listId))
                continue;

            var knockback = parts[5..].Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();

            var lut = parts[4] == "global"
                ? null
                : parts[4].Split(',').Select(point => point.Split(':')).Select(point =>
                    (float.Parse(point[0], CultureInfo.InvariantCulture), float.Parse(point[1], CultureInfo.InvariantCulture))).ToArray();

            if (!map.TryGetValue(listId, out var attacks))
                map[listId] = attacks = [];

            attacks.Add(new ListAttack(parts[1], parts[2], parts[3] == "1", lut, knockback[0], knockback[1], knockback[2], knockback[3]));
        }

        return map;
    }

    public static IReadOnlyList<ListAttack> Get(uint listId)
        => AttacksByList.Value.TryGetValue(listId, out var attacks) ? attacks : [];
}
