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

public readonly record struct SquadScoring(float AttackPriority, float NearZone, float FarZone, float HitRecently, float OutOfCombatZone);

public readonly record struct SquadLeader(int DesiredNearFighters, int DesiredFarFighters, float MinAngleToNextAi);

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

    public float HitEndTime => ChargeTime + ActiveTime * EndFactor;

    public float PlannedDistance(float elapsed)
    {
        var motion = Motion;
        var activeStart = motion.ActiveWindowOnly ? ChargeTime + ActiveTime * BeginFactor : ChargeTime;
        var activeEnd = motion.ActiveWindowOnly ? HitEndTime : ChargeTime + ActiveTime;

        return Part(motion.Charge, 0, ChargeTime) + Part(motion.Active, activeStart, activeEnd)
            + Part(motion.Recover, ChargeTime + ActiveTime, Duration);

        float Part(ChargeStage stage, float start, float end)
            => !stage.Move || stage.Distance == 0 ? 0
                : end <= start ? (elapsed >= end ? stage.Distance : 0)
                : stage.Distance * Math.Clamp((elapsed - start) / (end - start), 0f, 1f);
    }

    public AttackMotion Motion => CreatureAiTable.TryGetMotion(Action, out var motion) ? motion : AttackMotion.None;

    public float HitReach => Reach - Motion.OffsetZ;

    public float AttackRange => CreatureAiTable.TryGetProjectile(Action, out var launch) ? launch.Range : HitReach + Motion.Lunge;

    public ChargeStage StageAt(float elapsed)
    {
        var motion = Motion;

        if (elapsed < ChargeTime)
            return motion.Charge;

        if (elapsed < ChargeTime + ActiveTime)
        {
            var window = (elapsed - ChargeTime) / ActiveTime;
            return !motion.ActiveWindowOnly || window >= BeginFactor && window <= EndFactor ? motion.Active : motion.Active with { Distance = 0 };
        }

        return motion.Recover;
    }
}

public sealed record CreatureListEntry(float Weight, bool IsList, string Name, string[] Difficulties, bool InvertDifficulties,
    string[] WorldTypes, bool InvertWorldTypes)
{
    public bool Matches(string difficulty, string worldType)
        => Allows(Difficulties, InvertDifficulties, difficulty) && Allows(WorldTypes, InvertWorldTypes, worldType);

    private static bool Allows(string[] values, bool invert, string value)
        => values.Length == 0 || values.Contains(value, StringComparer.OrdinalIgnoreCase) != invert;
}

public sealed record CreatureList(string Category, CreatureListEntry[] Entries);

public sealed record MinionSpawn(int MinCount, int MaxCount, float MinRadius, float MaxRadius, float MinAngle, float MaxAngle, float HomeRange, float Roam);

public sealed record Summon(string List, string SpawnParams, string Type);

public readonly record struct ChargeStage(float Distance, bool Move, bool Rotate, bool StopWhenInRange);

public sealed record AttackMotion(float OffsetZ, float Arc, bool ActiveWindowOnly, ChargeStage Charge, ChargeStage Active, ChargeStage Recover)
{
    public static readonly AttackMotion None = new(0, 360, false, new(0, false, true, true), new(0, false, true, true), new(0, false, true, true));

    public float Lunge => (Charge.Move ? MathF.Max(Charge.Distance, 0) : 0) + (Active.Move ? MathF.Max(Active.Distance, 0) : 0);
}

public readonly record struct SpawnParams(float HomeRange, float Roam);

public sealed record SelectionTableRule(string List, string Condition);

public sealed record SelectionListRule(string[] Tags, string Condition, bool SelectIfTrue);

public sealed record RelationRule(string Matcher, AiReaction OnSight, AiReaction OnCloseRange, AiReaction OnHit)
{
    public bool Matches(CreatureAi other) => Matcher switch
    {
        "*" => true,
        _ when Matcher.StartsWith("type:") => other.EntityType is { } type
            && (type.Equals(Matcher[5..], StringComparison.OrdinalIgnoreCase) || type.StartsWith(Matcher[5..] + "-", StringComparison.OrdinalIgnoreCase)),
        _ when Matcher.StartsWith("bp:") => other.Name.Equals(Matcher[3..], StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}

public sealed record AiCondition(bool Invert, string Kind, string Argument);

public sealed record ProjectileLaunch(string Entity, uint DataIndex, uint DataCount, float Speed, float GravityFactor,
    float MinInaccuracy, float MaxInaccuracy, Vector3 Offset, float PitchOffset, float YawOffset, float Lifetime, float ImpactWeaponLevel, float ImpactCreatureLevel,
    float HomingMaxAngle, float SplashRadius, string[] ImpactActions)
{
    public float Range => GravityFactor > 0 ? Speed * Speed / (CreatureAiTable.WorldGravity * GravityFactor) : Speed * Lifetime;
}

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
        NearRangeMax = Number("nearmax");
        FarRangeMin = Number("farmin");
        FarRangeMax = Number("farmax");
        var squad = _fields["squad"].Split(':').Select(Parse).ToArray();
        Squad = new SquadScoring(squad[0], squad[1], squad[2], squad[3], squad[4]);
        var leader = _fields["leader"].Split(':').Select(Parse).ToArray();
        Leader = new SquadLeader((int)leader[0], (int)leader[1], leader[2]);
        TimeForHitRecently = Number("hitrecently");
        CircleTarget = _fields["circle"] == "1";
        AttackTimeModifier = Number("attackmod");
        MinNumForAttackTimeModification = (int)Number("attackmodmin");
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
        EntityType = _fields["type"] == "-" ? null : _fields["type"];
        BroadcastTime = Number("broadcast");
        BroadcastDelay = Number("broadcastdelay");
        IgnoreAfterTauntTime = Number("ignoretaunt");
        Selection = _fields["selection"] == "-" ? null : _fields["selection"];
        IsRangedFighter = _fields["ranged"] == "1";
        DodgeProbability = Number("dodge");
        DodgeDistance = Number("dodgedistance");
        DodgeSpeed = Number("dodgespeed");
        MinTimeBetweenDodges = Number("dodgegap");
        PrioritiseSideDodge = _fields["dodgeside"] == "1";
        RunFromHealth = Number("runfromhealth");
        AvoidanceRange = Number("avoidance");
        IsTerritorial = _fields["territorial"] == "Territorial";
        SummonCooldown = Number("summoncooldown");
        RelationsTable = _fields["relations"] == "-" ? null : _fields["relations"];
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
    public float NearRangeMax { get; }
    public float FarRangeMin { get; }
    public float FarRangeMax { get; }
    public SquadScoring Squad { get; }
    public SquadLeader Leader { get; }
    public float TimeForHitRecently { get; }
    public bool CircleTarget { get; }
    public float AttackTimeModifier { get; }
    public int MinNumForAttackTimeModification { get; }
    public bool HasZones => NearRangeMax > 0 && MathF.Max(FarRangeMin, FarRangeMax) > 0;
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
    public string? EntityType { get; }
    public string? EntityFamily => EntityType?.Split('-')[0];
    public float BroadcastTime { get; }
    public float BroadcastDelay { get; }
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
    public float AvoidanceRange { get; }
    public bool IsTerritorial { get; }
    public float SummonCooldown { get; }
    public string? RelationsTable { get; }

    public RelationRule? RelationTo(CreatureAi other)
        => RelationsTable is { } table && CreatureAiTable.TryGetRelations(table, out var rules) ? rules.FirstOrDefault(rule => rule.Matches(other)) : null;
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
        (float Input, float Output)[] CreatureDamageLut, Dictionary<string, List<RelationRule>> Relations, (float Spawn, float Respawn) Invisibility,
        Dictionary<string, SelectionTableRule[]> SelectionTables, Dictionary<string, SelectionListRule[]> SelectionLists,
        Dictionary<string, AiCondition> Conditions, Dictionary<string, float> BlockAngles, Dictionary<string, AttackMotion> Motions,
        Dictionary<string, CreatureList> CreatureLists, Dictionary<string, MinionSpawn> MinionSpawns, Dictionary<string, Summon> Summons);

    private static readonly Lazy<Data> Table = new(Load);

    private static Data Load()
    {
        var data = new Data([], new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), [],
            [], new(StringComparer.OrdinalIgnoreCase), default,
            new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase),
            new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase),
            new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "CreatureAi.txt");

        if (!File.Exists(path))
            return data;

        static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        static (float Min, float Max) Range(string field)
        {
            var bounds = field.Split('=')[1].Split(':');
            return (Number(bounds[0]), Number(bounds[1]));
        }

        static (string[] Values, bool Invert) Filter(string field)
            => field == "*" ? ([], false) : (field.TrimStart('!').Split(','), field.StartsWith('!'));

        static CreatureListEntry ListEntry(string field)
        {
            var entry = field.Split(':');
            var difficulties = Filter(entry[3]);
            var worldTypes = Filter(entry[4]);
            return new CreatureListEntry(Number(entry[0]), entry[1] == "list", entry[2], difficulties.Values, difficulties.Invert,
                worldTypes.Values, worldTypes.Invert);
        }

        static ChargeStage Stage(string field)
        {
            var parts = field.Split('=')[1].Split(':');
            return new ChargeStage(Number(parts[0]), parts[1] == "1", parts[2] == "1", parts[3] == "1");
        }

        static (float, float)[] Lut(string lut) => lut.Split(',').Select(point => point.Split(':')).Select(point => (Number(point[0]), Number(point[1]))).ToArray();

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts)
            {
                case ["damage", var lut]:
                    data = data with { DamageLut = Lut(lut) };
                    break;
                case ["invisibility", var onSpawn, var onRespawn]:
                    data = data with { Invisibility = (Number(onSpawn.Split('=')[1]), Number(onRespawn.Split('=')[1])) };
                    break;
                case ["damagecreature", var creatureLut]:
                    data = data with { CreatureDamageLut = Lut(creatureLut) };
                    break;
                case ["relation", var relationTable, var matcher, var sight, var close, var hit]:
                    if (!data.Relations.TryGetValue(relationTable, out var relationRules))
                        data.Relations[relationTable] = relationRules = [];
                    relationRules.Add(new RelationRule(matcher, Enum.Parse<AiReaction>(sight), Enum.Parse<AiReaction>(close), Enum.Parse<AiReaction>(hit)));
                    break;
                case ["spawnparams", var spawnName, var home, var roam]:
                    data.Spawns[spawnName] = new SpawnParams(Number(home.Split('=')[1]), Number(roam.Split('=')[1]));
                    break;
                case ["projectile", var launchAction, ..]:
                    var f = parts[2..].Select(field => field.Split('=', 2)).ToDictionary(field => field[0], field => field[1]);
                    data.Projectiles[launchAction] = new ProjectileLaunch(f["entity"], uint.Parse(f["dataindex"]), uint.Parse(f["datacount"]),
                        Number(f["speed"]), Number(f["gravity"]), Number(f["inaccmin"]), Number(f["inaccmax"]),
                        new Vector3(Number(f["offx"]), Number(f["offy"]), Number(f["offz"])), Number(f["pitch"]), Number(f["yaw"]),
                        Number(f["lifetime"]), Number(f["impactlevel"]), Number(f["impactcreature"]), Number(f["homing"]), Number(f["splash"]),
                        f["impact"] == "-" ? [] : f["impact"].Split(','));
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
                case ["creaturelist", var listName2, var category, ..]:
                    data.CreatureLists[listName2] = new CreatureList(category, [.. parts[3..].Select(ListEntry)]);
                    break;
                case ["minionspawn", var minionName, var count, var radius, var spawnAngle, var minionHome, var minionRoam]:
                    var counts = Range(count);
                    var radii = Range(radius);
                    var angles = Range(spawnAngle);
                    data.MinionSpawns[minionName] = new MinionSpawn((int)counts.Min, (int)counts.Max, radii.Min, radii.Max, angles.Min, angles.Max,
                        Number(minionHome.Split('=')[1]), Number(minionRoam.Split('=')[1]));
                    break;
                case ["summon", var summonAction, var summonList, var summonParams, var summonType]:
                    data.Summons[summonAction] = new Summon(summonList.Split('=')[1], summonParams.Split('=')[1], summonType.Split('=')[1]);
                    break;
                case ["attackmotion", var motionAction, var offz, var arc, var activeOnly, var chargeStage, var activeStage, var recoverStage]:
                    data.Motions[motionAction] = new AttackMotion(Number(offz.Split('=')[1]), Number(arc.Split('=')[1]), activeOnly.EndsWith("=1"),
                        Stage(chargeStage), Stage(activeStage), Stage(recoverStage));
                    break;
                case ["block", var blockAction, var angle]:
                    data.BlockAngles[blockAction] = Number(angle);
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

    public const float WorldGravity = 20f;

    public static (float Input, float Output)[] DamageLut => Table.Value.DamageLut;

    public static (float Input, float Output)[] CreatureDamageLut => Table.Value.CreatureDamageLut;

    public static (float Spawn, float Respawn) Invisibility => Table.Value.Invisibility;

    public static bool TryGetRelations(string table, out List<RelationRule> rules) => Table.Value.Relations.TryGetValue(table, out rules!);

    public static bool TryGetSelectionTable(string name, out SelectionTableRule[] rules) => Table.Value.SelectionTables.TryGetValue(name, out rules!);

    public static bool TryGetSelectionList(string name, out SelectionListRule[] rules) => Table.Value.SelectionLists.TryGetValue(name, out rules!);

    public static bool TryGetSummon(string action, out Summon summon) => Table.Value.Summons.TryGetValue(action, out summon!);

    public static bool TryGetMinionSpawn(string name, out MinionSpawn spawn) => Table.Value.MinionSpawns.TryGetValue(name, out spawn!);

    public static bool TryGetCreatureList(string name, out CreatureList list) => Table.Value.CreatureLists.TryGetValue(name, out list!);

    public static bool TryGetMotion(string action, out AttackMotion motion) => Table.Value.Motions.TryGetValue(action, out motion!);

    public static bool TryGetBlockAngle(string action, out float angle) => Table.Value.BlockAngles.TryGetValue(action, out angle);

    public static bool TryGetCondition(string name, out AiCondition condition) => Table.Value.Conditions.TryGetValue(name, out condition!);
}
