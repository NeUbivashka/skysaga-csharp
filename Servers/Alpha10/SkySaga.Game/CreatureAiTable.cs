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

public enum AiLoudness
{
    Quiet,
    Normal,
    Loud
}

public readonly record struct CreatureAbility(string Action, uint ActionCrc, uint PropertiesCrc, float Reach,
    float ChargeTime, float ActiveTime, float RecoverTime, float BeginFactor, float EndFactor, string[] Tags)
{
    public bool HasTag(string tag) => Array.Exists(Tags, t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));

    public float HitTime => ChargeTime + ActiveTime * BeginFactor;

    public float Duration => ChargeTime + ActiveTime + RecoverTime;
}

public readonly record struct SpawnParams(float HomeRange, float Roam);

public sealed record SelectionTableRule(string List, string Condition);

public sealed record SelectionListRule(string[] Tags, string Condition, bool SelectIfTrue);

public sealed record AiCondition(bool Invert, string Kind, string Argument);

public sealed record ProjectileLaunch(string Entity, uint DataIndex, uint DataCount, float Speed, float GravityFactor,
    float MinInaccuracy, float MaxInaccuracy, Vector3 Offset, float PitchOffset, float YawOffset, float Lifetime, float ImpactWeaponLevel);

public readonly record struct SleepTimes(float IdleMin, float IdleMax, float SleepMin, float SleepMax)
{
    public bool CanSleep => SleepMax > 0;
}

public readonly record struct SleepSegment(float Start, float End, SleepTimes Times)
{
    public bool Contains(float timeOfDay) => Start <= End
        ? timeOfDay >= Start && timeOfDay < End
        : timeOfDay >= Start || timeOfDay < End;
}

public sealed class CreatureAi
{
    private readonly Dictionary<string, string> _fields;

    public CreatureAi(string name, Dictionary<string, string> fields)
    {
        _fields = fields;
        Name = name;

        WalkSpeed = Number("walk");
        RunSpeed = Number("run");
        SprintSpeed = Number("sprint");
        JumpHeight = Number("jump");
        SeenDistance = Number("seen");
        SeenAngle = Number("seenangle");
        CloseDistance = Number("close");
        CloseAngle = Number("closeangle");
        WakeDistance = Number("wake");
        HuntTimeAfterHit = Number("hunt");
        NextAttackMinTime = Number("nextmin");
        NextAttackMaxTime = Number("nextmax");
        NearRangeMin = Number("nearmin");
        OnSight = Enum.Parse<AiReaction>(_fields["onsight"]);
        OnCloseRange = Enum.Parse<AiReaction>(_fields["onclose"]);
        OnHit = Enum.Parse<AiReaction>(_fields["onhit"]);
        OnHearing = Enum.Parse<AiReaction>(_fields["onhearing"]);
        HearingEvents = _fields["hearing"] == "-" ? [] : [.. _fields["hearing"].Split(',')];
        HearingRanges = [Number("quiet"), Number("normal"), Number("loud")];
        SleepHearingRanges = [Number("sleepquiet"), Number("sleepnormal"), Number("sleeploud")];
        AlertTime = Number("alert");
        LostTime = Number("lost");
        TauntTime = Number("taunt");
        IgnoreAfterTauntTime = Number("ignoretaunt");
        Selection = _fields["selection"] == "-" ? null : _fields["selection"];
        IsRangedFighter = _fields["ranged"] == "1";
        DodgeProbability = Number("dodge");
        DodgeDistance = Number("dodgedistance");
        DodgeSpeed = Number("dodgespeed");
        MinTimeBetweenDodges = Number("dodgegap");
        PrioritiseSideDodge = _fields["dodgeside"] == "1";
        RunFromHealth = Number("runfromhealth");
        ToAttack = _fields["toattack"];
        ToTarget = _fields["totarget"];
        BackOff = _fields["backoff"] == "-" ? null : _fields["backoff"];
        BackOffMinAngle = Number("backoffmin");
        BackOffMaxAngle = Number("backoffmax");
        BackOffLookAtTarget = _fields["backofflook"] == "1";
        var idle = _fields["idle"].Split(':');
        IdleMin = Parse(idle[0]);
        IdleMax = Parse(idle[1]);
        Sleep = ParseSleep(_fields["sleep"].Split(':'));
        Segments = _fields["segments"] == "-"
            ? []
            : [.. _fields["segments"].Split(';').Select(segment => segment.Split(':'))
                .Select(parts => new SleepSegment(Parse(parts[0]), Parse(parts[1]), ParseSleep(parts[2..])))];
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);

    private static SleepTimes ParseSleep(string[] parts) => new(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));

    private float Number(string key) => Parse(_fields[key]);

    public string Name { get; }
    public float WalkSpeed { get; }
    public float RunSpeed { get; }
    public float SprintSpeed { get; }
    public float JumpHeight { get; }
    public float SeenDistance { get; }
    public float SeenAngle { get; }
    public float CloseDistance { get; }
    public float CloseAngle { get; }
    public float WakeDistance { get; }
    public float HuntTimeAfterHit { get; }
    public float NextAttackMinTime { get; }
    public float NextAttackMaxTime { get; }
    public float NearRangeMin { get; }
    public AiReaction OnSight { get; }
    public AiReaction OnCloseRange { get; }
    public AiReaction OnHit { get; }
    public AiReaction OnHearing { get; }
    public HashSet<string> HearingEvents { get; }
    public float[] HearingRanges { get; }
    public float[] SleepHearingRanges { get; }
    public float AlertTime { get; }
    public float LostTime { get; }
    public float TauntTime { get; }
    public float IgnoreAfterTauntTime { get; }
    public string? Selection { get; }
    public bool IsRangedFighter { get; }
    public float DodgeProbability { get; }
    public float DodgeDistance { get; }
    public float DodgeSpeed { get; }
    public float MinTimeBetweenDodges { get; }
    public bool PrioritiseSideDodge { get; }
    public bool CanDodge => DodgeProbability > 0 && DodgeDistance > 0 && DodgeSpeed > 0;
    public float RunFromHealth { get; }
    public string ToAttack { get; }
    public string ToTarget { get; }
    public string? BackOff { get; }
    public float BackOffMinAngle { get; }
    public float BackOffMaxAngle { get; }
    public bool BackOffLookAtTarget { get; }
    public float IdleMin { get; }
    public float IdleMax { get; }
    public SleepTimes Sleep { get; }
    public SleepSegment[] Segments { get; }
    public List<CreatureAbility> Abilities { get; } = [];

    public IEnumerable<CreatureAbility> Tagged(string tag) => Abilities.Where(ability => ability.HasTag(tag));

    public float Speed(string movementType) => movementType switch
    {
        "Walk" => WalkSpeed,
        "Sprint" => SprintSpeed,
        _ => RunSpeed
    };

    public SleepTimes SleepAt(float timeOfDay)
    {
        foreach (var segment in Segments)
            if (segment.Contains(timeOfDay))
                return segment.Times;

        return Sleep;
    }
}

public static class CreatureAiTable
{
    private sealed record Data(Dictionary<uint, CreatureAi> Creatures, Dictionary<string, AiLoudness> Loudness,
        Dictionary<string, SpawnParams> Spawns, Dictionary<string, ProjectileLaunch> Projectiles, (float Input, float Output)[] DamageLut,
        Dictionary<string, SelectionTableRule[]> SelectionTables, Dictionary<string, SelectionListRule[]> SelectionLists,
        Dictionary<string, AiCondition> Conditions);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data([], new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), [],
            new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
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
                case ["spawnparams", var spawnName, var home, var roam]:
                    data.Spawns[spawnName] = new SpawnParams(Number(home.Split('=')[1]), Number(roam.Split('=')[1]));
                    break;
                case ["projectile", var launchAction, ..]:
                    var f = parts[2..].Select(field => field.Split('=', 2)).ToDictionary(field => field[0], field => field[1]);
                    data.Projectiles[launchAction] = new ProjectileLaunch(f["entity"], uint.Parse(f["dataindex"]), uint.Parse(f["datacount"]),
                        Number(f["speed"]), Number(f["gravity"]), Number(f["inaccmin"]), Number(f["inaccmax"]),
                        new Vector3(Number(f["offx"]), Number(f["offy"]), Number(f["offz"])), Number(f["pitch"]), Number(f["yaw"]),
                        Number(f["lifetime"]), Number(f["impactlevel"]));
                    break;
                case ["selectiontable", var tableName, ..]:
                    data.SelectionTables[tableName] = [.. parts[2..].Select(rule => rule.Split(':')).Select(rule => new SelectionTableRule(rule[0], rule[1]))];
                    break;
                case ["selectionlist", var listName, ..]:
                    data.SelectionLists[listName] = [.. parts[2..].Select(rule => rule.Split(':'))
                        .Select(rule => new SelectionListRule(rule[0].Split(','), rule[1], rule[2] == "1"))];
                    break;
                case ["condition", var conditionName, var invert, var kind, ..]:
                    data.Conditions[conditionName] = new AiCondition(invert == "1", kind, parts.Length > 4 ? parts[4] : "");
                    break;
                case ["loudness", var sensoryEvent, var loudness]:
                    data.Loudness[sensoryEvent] = Enum.Parse<AiLoudness>(loudness);
                    break;
                case ["creature", var name, ..]:
                    data.Creatures[Util.ComputeCrc32(name)] = new CreatureAi(name, parts[2..]
                        .Select(field => field.Split('=', 2))
                        .ToDictionary(field => field[0], field => field[1]));
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

    public static bool TryGetLoudness(string sensoryEvent, out AiLoudness loudness)
        => Table.Value.Loudness.TryGetValue(sensoryEvent, out loudness);

    public static bool TryGetProjectile(string action, out ProjectileLaunch launch) => Table.Value.Projectiles.TryGetValue(action, out launch!);

    public static bool TryGetSpawnParams(string name, out SpawnParams spawn) => Table.Value.Spawns.TryGetValue(name, out spawn);

    public static (float Input, float Output)[] DamageLut => Table.Value.DamageLut;

    public static bool TryGetSelectionTable(string name, out SelectionTableRule[] rules) => Table.Value.SelectionTables.TryGetValue(name, out rules!);

    public static bool TryGetSelectionList(string name, out SelectionListRule[] rules) => Table.Value.SelectionLists.TryGetValue(name, out rules!);

    public static bool TryGetCondition(string name, out AiCondition condition) => Table.Value.Conditions.TryGetValue(name, out condition!);
}
