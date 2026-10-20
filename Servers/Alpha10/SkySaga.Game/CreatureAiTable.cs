using System.Globalization;
using System.Linq;

namespace SkySaga.Game;

public enum AiReaction
{
    Ignore,
    Friendly,
    Aggressive,
    Scared
}

public readonly record struct CreatureAbility(string Action, uint ActionCrc, uint PropertiesCrc, float Reach,
    float ChargeTime, float ActiveTime, float RecoverTime, float BeginFactor, float EndFactor, string[] Tags)
{
    public bool HasTag(string tag) => Array.Exists(Tags, t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));

    public float HitTime => ChargeTime + ActiveTime * BeginFactor;

    public float Duration => ChargeTime + ActiveTime + RecoverTime;
}

public sealed record CreatureAi(float WalkSpeed, float RunSpeed, float JumpHeight,
    float SeenDistance, float SeenAngle, float CloseDistance, float CloseAngle, float HuntTimeAfterHit,
    float NextAttackMinTime, float NextAttackMaxTime, AiReaction OnSight, AiReaction OnCloseRange, AiReaction OnHit,
    float AlertTime, float LostTime, List<CreatureAbility> Abilities)
{
    public IEnumerable<CreatureAbility> Tagged(string tag) => Abilities.Where(ability => ability.HasTag(tag));
}

public static class CreatureAiTable
{
    private sealed record Data(Dictionary<uint, CreatureAi> Creatures, (float Input, float Output)[] DamageLut);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data([], []);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CreatureAi.txt");

        if (!File.Exists(path))
            return data;

        static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts)
            {
                case ["damage", var lut]:
                    data = data with
                    {
                        DamageLut = lut.Split(',').Select(point => point.Split(':')).Select(point => (Number(point[0]), Number(point[1]))).ToArray()
                    };
                    break;
                case ["creature", var name, ..] when parts.Length == 17:
                    data.Creatures[Util.ComputeCrc32(name)] = new CreatureAi(
                        Number(parts[2]), Number(parts[3]), Number(parts[4]),
                        Number(parts[5]), Number(parts[6]), Number(parts[7]), Number(parts[8]), Number(parts[9]),
                        Number(parts[10]), Number(parts[11]),
                        Enum.Parse<AiReaction>(parts[12]), Enum.Parse<AiReaction>(parts[13]), Enum.Parse<AiReaction>(parts[14]),
                        Number(parts[15]), Number(parts[16]), []);
                    break;
                case ["ability", var owner, var action, var properties, ..] when parts.Length == 11
                    && data.Creatures.TryGetValue(Util.ComputeCrc32(owner), out var creature):
                    creature.Abilities.Add(new CreatureAbility(action, Util.ComputeCrc32(action),
                        properties == "-" ? 0 : Util.ComputeCrc32(properties),
                        Number(parts[4]), Number(parts[5]), Number(parts[6]), Number(parts[7]), Number(parts[8]), Number(parts[9]),
                        parts[10] == "-" ? [] : parts[10].Split(',')));
                    break;
            }
        }

        return data;
    }

    public static bool TryGet(string entityName, out CreatureAi creature)
        => Table.Value.Creatures.TryGetValue(Util.ComputeCrc32(entityName), out creature!);

    public static (float Input, float Output)[] DamageLut => Table.Value.DamageLut;
}
