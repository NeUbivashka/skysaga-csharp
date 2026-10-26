using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public class AiManager
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
    private static readonly uint DefaultPerformerKey = Util.ComputeCrc32("Default Performer");

    private const byte StateIdle = 0;
    private const byte StateWalk = 3;
    private const byte StateRun = 4;
    private const byte StateSprint = 5;
    private const byte StateSwim = 6;
    private const byte StateDodge = 7;
    private const uint DodgeRight = 2;
    private const uint DodgeBack = 4;
    private const uint DodgeLeft = 6;
    private const byte MentalNormal = 0;
    private const byte MentalSleep = 3;
    private const float DirectionScale = 64f;
    private const float InputSpeedScale = 8f;
    private const string MeleeTag = "ActionMelee";
    private const string DefaultSpawnParams = "GenericFeatureSpawner";
    private const float ProjectileAimHeight = 1f;
    private const float Gravity = 20f;
    private const string ProjectileTag = "ActionProjectile";
    private const string AlertTag = "EmoteAlert";
    private const string LostTag = "EmoteLost";
    private const string VictoryTag = "EmoteVictory";
    private const string TauntTag = "EmoteTaunt";
    private const string CombatIdleTag = "EmoteCombatIdle";
    private const string BroadcastTag = "EmoteBroadcast";
    private const string BlockTag = "ActionBlock";
    private static readonly string[] AttackTags = ["ActionMelee", "ActionProjectile", "ActionShieldBreaker"];

    private static readonly Dictionary<byte, string> Footsteps = new()
    {
        [StateWalk] = "WalkingFootsteps",
        [StateRun] = "RunningFootsteps",
        [StateSprint] = "SprintingFootsteps",
        [StateSwim] = "Swim"
    };

    private static readonly Dictionary<int, Brain> Brains = [];
    private static readonly List<Sound> Sounds = [];
    private static readonly Dictionary<int, (ActionStats Action, DateTime Until)> PlayerActions = [];

    private readonly IWorldManager _worldManager;
    private readonly PlayerConnectionManager _playerConnectionManager;
    private readonly Random _random = new();
    private readonly Projectiles _projectiles;

    private DateTime _lastUpdate = DateTime.UtcNow;

    private enum SquadZone
    {
        Near,
        Far,
        OutOfCombat
    }

    private readonly record struct Sound(int SourceId, Vector3 Position, string Event);

    private sealed class Brain
    {
        public float FallSpeed;
        public required bool Free;
        public int TargetId;
        public AiReaction Mode;
        public DateTime HuntUntil;
        public DateTime NextAttack;
        public Attack? Current;
        public DateTime IdleSince;
        public DateTime? SleepAt;
        public DateTime? AsleepUntil;
        public Vector3? BackOffDirection;
        public bool Blocked;
        public float KeepRange;
        public required Vector3 Home;
        public required SpawnParams Spawn;
        public DateTime NextWander;
        public Vector3? WanderTarget;
        public bool ReturningHome;
        public bool JustHeard;
        public DateTime? TauntEndsAt;
        public int IgnoredTargetId;
        public DateTime IgnoreUntil;
        public bool CombatIdleDone;
        public CreatureAbility? Planned;
        public Vector3 DodgeDirection;
        public DateTime DodgeEndsAt;
        public DateTime NextDodge;
        public bool BroadcastPending;
        public DateTime NextBroadcast;
        public SquadZone Zone;
        public float HoldBearing;
        public DateTime HitAt;
    }

    private sealed class Attack
    {
        public required CreatureAbility Ability;
        public required DateTime HitAt;
        public required DateTime EndsAt;
        public bool Resolved;
    }

    public AiManager(IWorldManager worldManager, PlayerConnectionManager playerConnectionManager)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _playerConnectionManager = playerConnectionManager ?? throw new ArgumentNullException(nameof(playerConnectionManager));
        _projectiles = new Projectiles(worldManager, playerConnectionManager);
    }

    public static void Hit(Entity creature, Entity attacker)
    {
        if (!Brains.TryGetValue(creature.Id, out var brain) || !CreatureAiTable.TryGet(creature.Name, out var ai))
            return;

        Wake(creature, brain, DateTime.UtcNow);

        if (ai.OnHit is not (AiReaction.Aggressive or AiReaction.Scared))
            return;

        brain.TargetId = attacker.Id;
        brain.HitAt = DateTime.UtcNow;
        brain.IgnoredTargetId = 0;
        brain.Mode = ai.OnHit;
        brain.HuntUntil = DateTime.UtcNow.AddSeconds(ai.HuntTimeAfterHit);
    }

    public static bool TryDodge(PlayerConnectionManager connections, Entity creature, Entity attacker)
    {
        var now = DateTime.UtcNow;

        if (!Brains.TryGetValue(creature.Id, out var brain) || !CreatureAiTable.TryGet(creature.Name, out var ai) || !ai.CanDodge
            || brain.AsleepUntil is not null || brain.Current is not null || now < brain.NextDodge
            || Random.Shared.NextDouble() * 100 >= ai.DodgeProbability
            || !creature.TryGetComponent<SmoothedTransformComponent>(out var transform) || Combat.PositionOf(attacker) is not Vector3 from)
            return false;

        var toAttacker = Horizontal(from - transform.Position);

        if (toAttacker.LengthSquared() > 0.0001f)
            Face(transform, Vector3.Normalize(toAttacker));

        var forward = Forward(transform.YawDegrees);
        var right = new Vector3(forward.Z, 0f, -forward.X);
        var kind = !ai.PrioritiseSideDodge ? DodgeBack : Random.Shared.Next(2) == 0 ? DodgeRight : DodgeLeft;

        brain.DodgeDirection = kind switch
        {
            DodgeRight => right,
            DodgeLeft => -right,
            _ => -forward
        };
        brain.DodgeEndsAt = now.AddSeconds(ai.DodgeDistance / ai.DodgeSpeed);
        brain.NextDodge = brain.DodgeEndsAt.AddSeconds(ai.MinTimeBetweenDodges);
        brain.Planned = null;

        Hit(creature, attacker);

        connections.BroadcastToAll(new EntityDodged { EntityId = creature.Id, DodgeKind = kind });

        Console.WriteLine($"[AiManager] {creature.Name}#{creature.Id} dodges {(kind == DodgeRight ? "right" : kind == DodgeLeft ? "left" : "back")} from {attacker.Id}");
        return true;
    }

    public static void PlayerActionStarted(Entity player, ActionStats action)
        => PlayerActions[player.Id] = (action, DateTime.UtcNow.AddSeconds(action.Duration));

    public static void PlayerActionEnded(Entity player)
    {
        if (PlayerActions.TryGetValue(player.Id, out var current) && !current.Action.HasTag(BlockTag))
            PlayerActions.Remove(player.Id);
    }

    private static string[] TargetTags(int targetId, DateTime now)
        => PlayerActions.TryGetValue(targetId, out var current) && now < current.Until ? current.Action.Tags : [];

    public static bool TryBlock(Entity creature, Entity attacker)
    {
        if (!Brains.TryGetValue(creature.Id, out var brain) || brain.Current is not { } current || !current.Ability.HasTag(BlockTag)
            || DateTime.UtcNow >= current.EndsAt
            || !CreatureAiTable.TryGetBlockAngle(current.Ability.Action, out var angle)
            || !creature.TryGetComponent<SmoothedTransformComponent>(out var transform) || Combat.PositionOf(attacker) is not Vector3 from)
            return false;

        var toAttacker = Horizontal(from - transform.Position);
        var off = toAttacker.LengthSquared() > 0.0001f
            ? MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(toAttacker), Forward(transform.YawDegrees)), -1f, 1f)) * 180f / MathF.PI
            : 0f;

        if (off > angle / 2)
            return false;

        Hit(creature, attacker);

        Console.WriteLine($"[AiManager] {creature.Name}#{creature.Id} blocks {attacker.Id} with {current.Ability.Action} ({off:0} deg off its facing)");
        return true;
    }

    public static void Hear(Entity source, string sensoryEvent)
    {
        if (Combat.PositionOf(source) is Vector3 position)
            Sounds.Add(new Sound(source.Id, position, sensoryEvent));
    }

    public void Tick()
    {
        var now = DateTime.UtcNow;

        Knockback.Update(_worldManager, now);

        if (now - _lastUpdate < UpdateInterval)
            return;

        var delta = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        RemoveCorpses(now);
        _projectiles.Update(now, delta);

        var players = _playerConnectionManager.GetAllConnections()
            .Select(connection => connection.PlayerEntity)
            .Where(player => player.TryGetComponent<ClientHealthComponent>(out var health) && !health.IsDead && Combat.PositionOf(player) is not null)
            .ToList();

        foreach (var player in players)
            if (player.TryGetComponent<ClientCharacterStateComponent>(out var state) && Footsteps.TryGetValue(state.CharacterStateId, out var footsteps))
                Hear(player, footsteps);

        var sounds = Sounds.ToList();
        Sounds.Clear();

        var timeOfDay = TimeOfDay();

        AssignSquads(now);

        foreach (var entity in _worldManager.EntityManager.Entities.ToList())
        {
            if (!entity.TryGetComponent<ClientEnemyAiComponent>(out var component)
                || !entity.TryGetComponent<SmoothedTransformComponent>(out var transform)
                || !CreatureAiTable.TryGet(entity.Name, out var ai))
                continue;

            if (!Brains.TryGetValue(entity.Id, out var brain))
                Brains[entity.Id] = brain = new Brain
                {
                    Home = _worldManager.MapSpawnOf(entity.Id)?.Position ?? transform.Position,
                    Spawn = CreatureAiTable.TryGetSpawnParams(_worldManager.MapSpawnOf(entity.Id)?.SpawnParams ?? DefaultSpawnParams, out var spawn) ? spawn : default,
                    NextWander = now.AddSeconds(Between(ai.IdleMin, ai.IdleMax)),
                    Free = !entity.TryGetComponent<CharacterStateComponent>(out var initial) || initial.CharacterMentalStateId == MentalNormal,
                    IdleSince = now
                };

            if (entity.TryGetComponent<ClientHealthComponent>(out var health) && health.IsDead || Knockback.IsActive(entity.Id))
            {
                brain.Current = null;
                Stop(entity);
                continue;
            }

            if (Fall(transform, brain, delta))
            {
                Stop(entity);
                continue;
            }

            if (now < brain.DodgeEndsAt)
            {
                Dodge(entity, transform, ai, brain, delta);
                continue;
            }

            if (brain.TargetId != 0 && ai.RunFromHealth > 0 && health is not null && health.MaxHP > 0
                && (health.SyncedFullHPValues ?? health.MaxHP) < ai.RunFromHealth * health.MaxHP)
                brain.Mode = AiReaction.Scared;

            Think(entity, component, transform, ai, brain, players, sounds, timeOfDay, now, delta);
        }
    }

    private void Think(Entity entity, ClientEnemyAiComponent component, TransformComponent transform, CreatureAi ai, Brain brain,
        List<Entity> players, List<Sound> sounds, float timeOfDay, DateTime now, float delta)
    {
        if (brain.Current is Attack attack)
        {
            Stop(entity);

            if (TargetPosition(brain, players) is Vector3 position)
                Face(transform, Horizontal(position - transform.Position));

            if (attack.Ability.HasTag(ProjectileTag) && !attack.Resolved && now >= attack.HitAt)
            {
                attack.Resolved = true;

                if (CreatureAiTable.TryGetProjectile(attack.Ability.Action, out var launch) && TargetPosition(brain, players) is Vector3 aim)
                    _projectiles.Launch(entity, transform.Position, transform.YawDegrees, aim + Vector3.UnitY * ProjectileAimHeight, attack.Ability, launch, now);
            }

            if (attack.Ability.HasTag(MeleeTag) && !attack.Resolved && now >= attack.HitAt)
            {
                attack.Resolved = true;

                if (players.FirstOrDefault(player => player.Id == brain.TargetId) is { } victim
                    && Vector3.Distance(Combat.PositionOf(victim)!.Value, transform.Position) <= attack.Ability.Reach)
                    Combat.CreatureAttack(_playerConnectionManager, _worldManager.EntityManager, entity, victim, attack.Ability);
            }

            if (now < attack.EndsAt)
                return;

            brain.Current = null;

            if (attack.Ability.HasTag(MeleeTag) || attack.Ability.HasTag(ProjectileTag))
            {
                brain.NextAttack = now.AddSeconds(ai.NextAttackMinTime + _random.NextDouble() * (ai.NextAttackMaxTime - ai.NextAttackMinTime));
                brain.BackOffDirection = ai.BackOff is null || TargetPosition(brain, players) is not Vector3 from
                    ? null
                    : BackOffDirection(ai, Horizontal(transform.Position - from));
            }
        }

        if (brain.TargetId != 0 && !players.Any(player => player.Id == brain.TargetId)
            && _worldManager.EntityManager.TryGetEntity(brain.TargetId, out var fallen)
            && fallen.TryGetComponent<ClientHealthComponent>(out var fallenHealth) && fallenHealth.IsDead)
        {
            var aggressive = brain.Mode == AiReaction.Aggressive;
            Forget(component, brain, now);

            if (aggressive && Emote(entity, transform, ai, brain, VictoryTag, 0, now))
                return;
        }

        if (brain.AsleepUntil is DateTime asleepUntil)
        {
            if (now >= asleepUntil || players.Any(player => Distance(player, transform) <= ai.WakeDistance))
                Wake(entity, brain, now);
            else if (Heard(transform, ai, brain, sounds, ai.SleepHearingRanges, now) is null)
                return;
            else
                Wake(entity, brain, now);
        }

        if (brain.TargetId != 0 && brain.Spawn.HomeRange > 0 && Horizontal(transform.Position - brain.Home).Length() > brain.Spawn.HomeRange)
        {
            Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} is {Horizontal(transform.Position - brain.Home).Length():0.#} m from home, returning");
            Forget(component, brain, now);
            brain.ReturningHome = true;
        }

        if (brain.ReturningHome)
        {
            var toHome = Horizontal(brain.Home - transform.Position);

            if (toHome.Length() > Math.Max(brain.Spawn.Roam, ai.RunSpeed * delta) && !brain.Blocked)
            {
                Move(entity, transform, ai, brain, Vector3.Normalize(toHome), ai.Speed(ai.ToTarget), State(ai.ToTarget), delta);
                return;
            }

            brain.ReturningHome = false;
            brain.Blocked = false;
            brain.IdleSince = now;
            brain.NextWander = now.AddSeconds(Between(ai.IdleMin, ai.IdleMax));
        }

        var hadTarget = brain.TargetId != 0;
        var target = Target(transform, ai, brain, players, now) ?? Heard(transform, ai, brain, sounds, ai.HearingRanges, now);

        component.TargetEntityId = target?.Id ?? 0;

        if (target is null || Combat.PositionOf(target) is not Vector3 targetPosition)
        {
            Stop(entity);

            if (hadTarget)
            {
                brain.IdleSince = now;
                brain.SleepAt = null;
                Emote(entity, transform, ai, brain, LostTag, ai.LostTime, now);
                return;
            }

            Rest(entity, ai, brain, timeOfDay, now);

            if (brain.AsleepUntil is null)
                Wander(entity, transform, ai, brain, now, delta);

            return;
        }

        brain.SleepAt = null;
        brain.WanderTarget = null;

        var offset = Horizontal(targetPosition - transform.Position);
        var distance = offset.Length();
        var toTarget = distance > 0.0001f ? offset / distance : Forward(transform.YawDegrees);

        if (!hadTarget && brain.Mode == AiReaction.Aggressive)
        {
            Face(transform, toTarget);

            brain.BroadcastPending = true;

            if (Emote(entity, transform, ai, brain, AlertTag, ai.AlertTime, now))
                return;
        }

        if (brain.BroadcastPending)
        {
            brain.BroadcastPending = false;

            if (now >= brain.NextBroadcast && Emote(entity, transform, ai, brain, BroadcastTag, ai.BroadcastTime, now))
            {
                brain.NextBroadcast = now.AddSeconds(ai.BroadcastDelay);
                CallAllies(entity, transform, ai, target, now);
                return;
            }
        }

        if (brain.Mode == AiReaction.Scared)
        {
            Move(entity, transform, ai, brain, -toTarget, ai.RunSpeed, StateRun, delta);
            return;
        }

        var ready = now >= brain.NextAttack;

        if (brain.BackOffDirection is Vector3 away)
        {
            if (!ready && distance < ai.NearRangeMin)
            {
                Move(entity, transform, ai, brain, away, ai.Speed(ai.BackOff!), State(ai.BackOff!), delta);

                if (ai.BackOffLookAtTarget)
                    Face(transform, toTarget);

                return;
            }

            brain.BackOffDirection = null;
        }

        var reach = Vector3.Distance(targetPosition, transform.Position);

        if (ai.Tagged(MeleeTag).Any(ability => distance <= ability.Reach && reach > ability.Reach))
            brain.Blocked = true;

        var behaviour = brain.JustHeard ? AiBehaviour.SensoryEventResponse
            : ai.IsRangedFighter ? AiBehaviour.Ranged
            : brain.Blocked ? AiBehaviour.Taunt
            : AiBehaviour.Melee;

        brain.JustHeard = false;

        if (brain.TauntEndsAt is DateTime tauntEnd && now >= tauntEnd)
        {
            Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} cannot reach {brain.TargetId}, ignores it for {ai.IgnoreAfterTauntTime:0.#} s");
            brain.IgnoredTargetId = brain.TargetId;
            brain.IgnoreUntil = now.AddSeconds(ai.IgnoreAfterTauntTime);
            brain.Blocked = false;
            Forget(component, brain, now);
            Emote(entity, transform, ai, brain, LostTag, ai.LostTime, now);
            return;
        }

        var targetTags = TargetTags(target.Id, now);
        var selected = AbilitySelection.Select(ai, new SelectionContext(behaviour, reach, !brain.Blocked, targetTags));

        if (brain.Zone != SquadZone.Near)
            selected = [.. selected.Where(ability => !ability.HasTag(MeleeTag))];

        var wait = (float)(brain.NextAttack - now).TotalSeconds;

        if (!ready && brain.Zone == SquadZone.Near && targetTags.Any(tag => AttackTags.Contains(tag))
            && selected.FirstOrDefault(ability => ability.HasTag(BlockTag)) is CreatureAbility shield)
        {
            Stop(entity);
            Face(transform, toTarget);
            Perform(entity, transform, brain, shield, toTarget, wait, now);
            return;
        }

        if (ready && (brain.Planned is not CreatureAbility planned || !selected.Contains(planned)))
            brain.Planned = selected.Count > 0 ? selected[_random.Next(selected.Count)] : null;

        var inReach = brain.Planned is CreatureAbility plan
            ? !IsAttack(plan) || reach <= plan.Reach
            : selected.Any(ability => IsAttack(ability) && reach <= ability.Reach);

        if (inReach)
        {
            Stop(entity);
            Face(transform, toTarget);

            if (ready && brain.Planned is CreatureAbility chosen)
            {
                brain.Planned = null;
                brain.CombatIdleDone = false;
                Perform(entity, transform, brain, chosen, toTarget, 0, now);

                if (behaviour == AiBehaviour.Taunt)
                    brain.TauntEndsAt = now.AddSeconds(chosen.Duration);
                else
                    brain.Blocked = false;
            }
            else
                CombatIdle(entity, transform, ai, brain, now);

            return;
        }

        if (behaviour == AiBehaviour.Taunt)
        {
            Stop(entity);
            Face(transform, toTarget);
            Taunt(entity, transform, ai, brain, ready, now);
            return;
        }

        if (brain.Zone != SquadZone.Near)
        {
            Hold(entity, transform, ai, brain, targetPosition, toTarget, distance, delta);
            return;
        }

        if (!selected.Any(IsAttack))
        {
            Stop(entity);
            Face(transform, toTarget);
            return;
        }

        var movement = ready ? ai.ToAttack : ai.ToTarget;
        Move(entity, transform, ai, brain, toTarget, ai.Speed(movement), State(movement), delta);
    }

    private void AssignSquads(DateTime now)
    {
        var squads = new Dictionary<int, List<(Entity Entity, CreatureAi Ai, Brain Brain, Vector3 Position, float Priority)>>();

        foreach (var (id, brain) in Brains)
        {
            if (brain.TargetId == 0 || brain.Mode != AiReaction.Aggressive
                || !_worldManager.EntityManager.TryGetEntity(id, out var entity)
                || !CreatureAiTable.TryGet(entity.Name, out var ai)
                || !entity.TryGetComponent<SmoothedTransformComponent>(out var transform))
            {
                brain.Zone = SquadZone.Near;
                continue;
            }

            var hitRecently = now - brain.HitAt < TimeSpan.FromSeconds(ai.TimeForHitRecently);
            var priority = ai.Squad.AttackPriority + (hitRecently ? ai.Squad.HitRecently : 0);

            if (!squads.TryGetValue(brain.TargetId, out var squad))
                squads[brain.TargetId] = squad = [];

            squad.Add((entity, ai, brain, transform.Position, priority));
        }

        foreach (var (targetId, squad) in squads)
        {
            if (!_worldManager.EntityManager.TryGetEntity(targetId, out var target) || Combat.PositionOf(target) is not Vector3 targetPosition)
                continue;

            var ordered = squad
                .OrderByDescending(member => member.Priority)
                .ThenBy(member => member.Brain.Zone)
                .ThenBy(member => Horizontal(member.Position - targetPosition).Length())
                .ToList();
            var leader = ordered[0].Ai.Leader;
            var near = 0;
            var far = 0;
            var holders = new List<(Brain Brain, float Bearing)>();

            foreach (var (entity, ai, brain, position, _) in ordered)
            {
                var zone = SquadZone.Near;

                if (ai.HasZones)
                {
                    var best = float.MinValue;

                    foreach (var (candidate, score, open) in new[]
                             {
                                 (SquadZone.Near, ai.Squad.NearZone, near < leader.DesiredNearFighters),
                                 (SquadZone.Far, ai.Squad.FarZone, far < leader.DesiredFarFighters),
                                 (SquadZone.OutOfCombat, ai.Squad.OutOfCombatZone, true)
                             })
                    {
                        if (open && score > best)
                            (zone, best) = (candidate, score);
                    }
                }

                if (zone == SquadZone.Near)
                    near++;
                else if (zone == SquadZone.Far)
                    far++;

                if (zone != brain.Zone)
                    Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} squad zone {zone} (target {targetId}, near {near}/{leader.DesiredNearFighters}, far {far}/{leader.DesiredFarFighters})");

                brain.Zone = zone;

                if (zone != SquadZone.Near)
                {
                    var offset = Horizontal(position - targetPosition);
                    holders.Add((brain, MathF.Atan2(offset.X, offset.Z)));
                }
            }

            var spread = leader.MinAngleToNextAi * MathF.PI / 180f;
            var previous = float.NegativeInfinity;

            foreach (var (brain, bearing) in holders.OrderBy(holder => holder.Bearing))
            {
                brain.HoldBearing = MathF.Max(bearing, previous + spread);
                previous = brain.HoldBearing;
            }
        }
    }

    private void Hold(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, Vector3 targetPosition, Vector3 toTarget, float distance, float delta)
    {
        var inner = MathF.Min(ai.FarRangeMin, ai.FarRangeMax);
        var outer = MathF.Max(ai.FarRangeMin, ai.FarRangeMax);
        var wanted = brain.Zone == SquadZone.Far
            ? distance < inner || distance > outer ? (inner + outer) / 2 : distance
            : MathF.Max(distance, outer);
        var point = targetPosition + new Vector3(MathF.Sin(brain.HoldBearing), 0f, MathF.Cos(brain.HoldBearing)) * wanted;
        var toPoint = Horizontal(point - transform.Position);
        var speed = ai.Speed(ai.ToTarget);

        if (toPoint.Length() <= speed * delta)
        {
            Stop(entity);
            Face(transform, toTarget);
            return;
        }

        Move(entity, transform, ai, brain, Vector3.Normalize(toPoint), speed, State(ai.ToTarget), delta);
    }

    private void Taunt(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, bool ready, DateTime now)
    {
        if (!ready || brain.TauntEndsAt is not null)
            return;

        brain.TauntEndsAt = now.AddSeconds(ai.TauntTime);
        Emote(entity, transform, ai, brain, TauntTag, ai.TauntTime, now);
    }

    private void CombatIdle(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, DateTime now)
    {
        if (brain.CombatIdleDone)
            return;

        brain.CombatIdleDone = true;

        var idles = ai.Tagged(CombatIdleTag).ToList();
        var wait = (float)(brain.NextAttack - now).TotalSeconds;

        if (idles.Count > 0 && wait > 0)
            Perform(entity, transform, brain, idles[_random.Next(idles.Count)], Forward(transform.YawDegrees), wait, now);
    }

    private static bool IsAttack(CreatureAbility ability) => ability.HasTag(MeleeTag) || ability.HasTag(ProjectileTag);

    private void Rest(Entity entity, CreatureAi ai, Brain brain, float timeOfDay, DateTime now)
    {
        var sleep = ai.SleepAt(timeOfDay);

        if (!brain.Free || !sleep.CanSleep)
        {
            brain.SleepAt = null;
            return;
        }

        brain.SleepAt ??= brain.IdleSince.AddSeconds(Between(sleep.IdleMin, sleep.IdleMax));

        if (now < brain.SleepAt)
            return;

        brain.AsleepUntil = now.AddSeconds(Between(sleep.SleepMin, sleep.SleepMax));
        brain.SleepAt = null;

        if (entity.TryGetComponent<CharacterStateComponent>(out var state))
            state.CharacterMentalStateId = MentalSleep;

        Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} falls asleep until {brain.AsleepUntil:HH:mm:ss}");
    }

    private void Wander(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, DateTime now, float delta)
    {
        if (brain.Spawn.Roam <= 0 || ai.WalkSpeed <= 0)
            return;

        if (brain.WanderTarget is null)
        {
            if (now < brain.NextWander)
                return;

            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var radius = brain.Spawn.Roam * MathF.Sqrt((float)_random.NextDouble());
            brain.WanderTarget = brain.Home + new Vector3(MathF.Sin(angle) * radius, 0f, MathF.Cos(angle) * radius);
            brain.Blocked = false;
        }

        var offset = Horizontal(brain.WanderTarget.Value - transform.Position);
        var step = ai.WalkSpeed * delta;

        if (offset.Length() <= step || brain.Blocked)
        {
            brain.WanderTarget = null;
            brain.Blocked = false;
            brain.NextWander = now.AddSeconds(Between(ai.IdleMin, ai.IdleMax));
            Stop(entity);
            return;
        }

        Move(entity, transform, ai, brain, Vector3.Normalize(offset), ai.WalkSpeed, StateWalk, delta);
    }

    private static void Wake(Entity entity, Brain brain, DateTime now)
    {
        if (brain.AsleepUntil is null)
            return;

        brain.AsleepUntil = null;
        brain.IdleSince = now;

        if (entity.TryGetComponent<CharacterStateComponent>(out var state))
            state.CharacterMentalStateId = MentalNormal;

        Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} wakes up");
    }

    private Entity? Heard(TransformComponent transform, CreatureAi ai, Brain brain, List<Sound> sounds, float[] ranges, DateTime now)
    {
        if (ai.OnHearing is not (AiReaction.Aggressive or AiReaction.Scared))
            return null;

        foreach (var sound in sounds)
        {
            if (!ai.HearingEvents.Contains(sound.Event)
                || !CreatureAiTable.TryGetLoudness(sound.Event, out var loudness)
                || Vector3.Distance(sound.Position, transform.Position) > ranges[(int)loudness]
                || !_worldManager.EntityManager.TryGetEntity(sound.SourceId, out var source)
                || !source.TryGetComponent<ClientPlayerAspectsComponent>(out _)
                || source.Id == brain.IgnoredTargetId && now < brain.IgnoreUntil)
                continue;

            brain.TargetId = source.Id;
            brain.Mode = ai.OnHearing;
            brain.HuntUntil = now;
            brain.KeepRange = ranges[(int)loudness];
            brain.JustHeard = true;

            Console.WriteLine($"[AiManager] {ai.Name} hears {sound.Event} ({loudness}) of {source.Id} at {Vector3.Distance(sound.Position, transform.Position):0.#} m: {ai.OnHearing}");

            return source;
        }

        return null;
    }

    private void CallAllies(Entity caller, TransformComponent from, CreatureAi ai, Entity target, DateTime now)
    {
        if (ai.EntityFamily is not string family)
            return;

        foreach (var entity in _worldManager.EntityManager.Entities)
        {
            if (entity.Id == caller.Id
                || !Brains.TryGetValue(entity.Id, out var brain) || brain.TargetId != 0 || brain.ReturningHome
                || !CreatureAiTable.TryGet(entity.Name, out var ally) || ally.EntityFamily != family
                || ally.OnHearing is not (AiReaction.Aggressive or AiReaction.Scared)
                || !entity.TryGetComponent<SmoothedTransformComponent>(out var transform)
                || entity.TryGetComponent<ClientHealthComponent>(out var health) && health.IsDead
                || target.Id == brain.IgnoredTargetId && now < brain.IgnoreUntil)
                continue;

            var range = (brain.AsleepUntil is null ? ally.HearingRanges : ally.SleepHearingRanges)[(int)AiLoudness.Loud];
            var distance = Vector3.Distance(transform.Position, from.Position);

            if (distance > range)
                continue;

            Wake(entity, brain, now);
            brain.TargetId = target.Id;
            brain.Mode = ally.OnHearing;
            brain.HuntUntil = now;
            brain.KeepRange = Combat.PositionOf(target) is Vector3 targetPosition
                ? MathF.Max(range, Vector3.Distance(targetPosition, transform.Position))
                : range;
            brain.JustHeard = true;

            Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} answers the call of {caller.Name}#{caller.Id} at {distance:0.#} m: {ally.OnHearing} on {target.Id}");
        }
    }

    private Vector3 BackOffDirection(CreatureAi ai, Vector3 away)
    {
        if (away.LengthSquared() < 0.0001f)
            return Vector3.Zero;

        var angle = Between(ai.BackOffMinAngle, ai.BackOffMaxAngle) * (_random.Next(2) == 0 ? -1 : 1) * MathF.PI / 180f;
        var direction = Vector3.Normalize(away);
        var (sin, cos) = MathF.SinCos(angle);

        return new Vector3(direction.X * cos - direction.Z * sin, 0f, direction.X * sin + direction.Z * cos);
    }

    private float Between(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private static byte State(string movementType) => movementType switch
    {
        "Walk" => StateWalk,
        "Sprint" => StateSprint,
        _ => StateRun
    };

    private static float Distance(Entity player, TransformComponent transform)
        => Vector3.Distance(Combat.PositionOf(player)!.Value, transform.Position);

    private bool Emote(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, string tag, float time, DateTime now)
    {
        var emotes = ai.Tagged(tag).ToList();

        if (emotes.Count == 0)
            return false;

        Perform(entity, transform, brain, emotes[_random.Next(emotes.Count)], Forward(transform.YawDegrees), time, now);
        return true;
    }

    private static void Forget(ClientEnemyAiComponent component, Brain brain, DateTime now)
    {
        brain.TargetId = 0;
        brain.Mode = AiReaction.Ignore;
        brain.BackOffDirection = null;
        brain.WanderTarget = null;
        brain.Planned = null;
        brain.TauntEndsAt = null;
        brain.IdleSince = now;
        component.TargetEntityId = 0;
    }

    private static Vector3? TargetPosition(Brain brain, List<Entity> players)
        => players.FirstOrDefault(player => player.Id == brain.TargetId) is { } target ? Combat.PositionOf(target) : null;

    private static Vector3 Horizontal(Vector3 value) => new(value.X, 0f, value.Z);

    private static Entity? Target(TransformComponent transform, CreatureAi ai, Brain brain, List<Entity> players, DateTime now)
    {
        if (brain.Mode != AiReaction.Ignore)
        {
            var current = players.FirstOrDefault(player => player.Id == brain.TargetId);

            if (current is not null && Combat.PositionOf(current) is Vector3 position
                && (now < brain.HuntUntil || Vector3.Distance(position, transform.Position) <= MathF.Max(ai.SeenDistance, brain.KeepRange)))
                return current;

            brain.TargetId = 0;
            brain.Mode = AiReaction.Ignore;
            brain.BackOffDirection = null;
            return null;
        }

        foreach (var player in players)
        {
            if (player.Id == brain.IgnoredTargetId && now < brain.IgnoreUntil)
                continue;

            var offset = Combat.PositionOf(player)!.Value - transform.Position;
            var distance = offset.Length();
            offset.Y = 0f;

            var angle = offset.LengthSquared() > 0.0001f
                ? MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(offset), Forward(transform.YawDegrees)), -1f, 1f)) * 180f / MathF.PI
                : 0f;

            var reaction = distance <= ai.CloseDistance && angle <= ai.CloseAngle / 2 ? ai.OnCloseRange
                : distance <= ai.SeenDistance && angle <= ai.SeenAngle / 2 ? ai.OnSight
                : AiReaction.Ignore;

            if (reaction is not (AiReaction.Aggressive or AiReaction.Scared))
                continue;

            brain.TargetId = player.Id;
            brain.Mode = reaction;
            brain.HuntUntil = now;
            brain.KeepRange = 0;

            Console.WriteLine($"[AiManager] {ai.Name} {(distance <= ai.CloseDistance && angle <= ai.CloseAngle / 2 ? "notices close" : "sees")} {player.Id} at {distance:0.#} m, {angle:0} deg: {reaction}");

            return player;
        }

        return null;
    }

    private void Perform(Entity entity, TransformComponent transform, Brain brain, CreatureAbility ability, Vector3 direction, float time, DateTime now)
    {
        brain.Current = new Attack
        {
            Ability = ability,
            HitAt = now.AddSeconds(ability.HitTime),
            EndsAt = now.AddSeconds(time > 0 ? time : ability.Duration)
        };

        _playerConnectionManager.BroadcastToAll(new EntityPerformedAbility
        {
            EntityId = entity.Id,
            PerformerKey = DefaultPerformerKey,
            PropertiesCrc = ability.PropertiesCrc == 0 ? null : ability.PropertiesCrc,
            Position = transform.Position,
            Direction = new Vector3Int((int)MathF.Round(direction.X * DirectionScale), 0, (int)MathF.Round(direction.Z * DirectionScale)),
            Power = 1f,
            Impact = new AbilityTarget { Flag = true },
            ActionCrc = ability.ActionCrc,
            Flag = true
        });

        Console.WriteLine($"[AiManager] {entity.Name}#{entity.Id} performs {ability.Action} ({string.Join(",", ability.Tags)}) target {brain.TargetId}");
    }

    private void Move(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, Vector3 direction, float speed, byte state, float delta)
    {
        Face(transform, direction);

        var next = transform.Position + direction * speed * delta;
        var ground = SpawnFinder.GroundBelow(_worldManager.ChunkManager, next, ai.JumpHeight);

        if (ground is not float groundY || groundY - transform.Position.Y > ai.JumpHeight)
        {
            brain.Blocked = true;
            Stop(entity);
            return;
        }

        brain.Blocked = false;

        transform.Position = new Vector3(next.X, MathF.Max(groundY, transform.Position.Y), next.Z);

        SetMovement(entity, direction, speed, state);
    }

    private void Dodge(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, float delta)
    {
        var next = transform.Position + brain.DodgeDirection * ai.DodgeSpeed * delta;
        var ground = SpawnFinder.GroundBelow(_worldManager.ChunkManager, next, ai.JumpHeight);

        if (ground is not float groundY || groundY - transform.Position.Y > ai.JumpHeight)
        {
            brain.DodgeEndsAt = DateTime.MinValue;
            Stop(entity);
            return;
        }

        transform.Position = new Vector3(next.X, MathF.Max(groundY, transform.Position.Y), next.Z);
        SetMovement(entity, brain.DodgeDirection, ai.DodgeSpeed, StateDodge);
    }

    private static void Stop(Entity entity) => SetMovement(entity, Vector3.Zero, 0f, StateIdle);

    private static void SetMovement(Entity entity, Vector3 direction, float speed, byte state)
    {
        if (entity.TryGetComponent<ClientCharacterPhysicsComponent>(out var physics))
        {
            physics.InputMovementDirection = ((int)MathF.Round(direction.X * DirectionScale), 0, (int)MathF.Round(direction.Z * DirectionScale));
            physics.InputMovementSpeed = (uint)MathF.Round(speed * InputSpeedScale);
        }

        if (entity.TryGetComponent<CharacterStateComponent>(out var characterState))
            characterState.CharacterStateId = state;
    }

    private static void Face(TransformComponent transform, Vector3 direction)
    {
        if (direction.LengthSquared() > 0.0001f)
            transform.YawDegrees = MathF.Atan2(direction.X, direction.Z) * 180f / MathF.PI;
    }

    private static Vector3 Forward(float yawDegrees)
    {
        var yaw = yawDegrees * MathF.PI / 180f;
        return new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
    }

    private float TimeOfDay()
    {
        var timeOfDay = _worldManager.EntityManager.Entities
            .Select(entity => entity.TryGetComponent<ClientTimeOfDayComponent>(out var component) ? component : null)
            .FirstOrDefault(component => component is not null);

        return timeOfDay is null ? 0f : timeOfDay.CurrentTime(RakNet.RakNet.GetTime()) / (float)TimeOfDayComponent.DayLength;
    }

    private bool Fall(TransformComponent transform, Brain brain, float delta)
    {
        var position = transform.Position;

        if (SpawnFinder.GroundBelow(_worldManager.ChunkManager, position, 0f) is not float ground)
            return false;

        if (position.Y <= ground)
        {
            brain.FallSpeed = 0f;

            if (position.Y < ground)
                transform.Position = position with { Y = ground };

            return false;
        }

        brain.FallSpeed += Gravity * delta;
        transform.Position = position with { Y = MathF.Max(ground, position.Y - brain.FallSpeed * delta) };

        return true;
    }

    private void RemoveCorpses(DateTime now)
    {
        var corpses = _worldManager.EntityManager.Entities
            .Where(entity => entity.TryGetComponent<ClientEnemyAiComponent>(out _)
                && entity.TryGetComponent<ClientHealthComponent>(out var health)
                && health.IsDead
                && now - health.DiedUtc >= Combat.CorpseLifetime)
            .ToList();

        foreach (var corpse in corpses)
        {
            var removed = corpse.TryGetComponent<ClientInventoryComponent>(out var inventory)
                ? inventory.InventoryEntityList.Where(id => id != 0).Append(corpse.Id).ToList()
                : [corpse.Id];

            foreach (var id in removed)
            {
                _worldManager.EntityManager.RemoveEntity(id);
                _playerConnectionManager.BroadcastToAll(new EntityRemoved { Id = id });
            }

            Brains.Remove(corpse.Id);
            Knockback.Cancel(corpse.Id);

            Console.WriteLine($"[AiManager] Removed the corpse of {corpse.Name}#{corpse.Id}.");

            foreach (var entity in _worldManager.RespawnMapCreature(corpse))
            {
                _playerConnectionManager.BroadcastToAll(new EntityAdd
                {
                    Id = entity.Id,
                    NameHash = Util.ComputeCrc32(entity.Name),
                    SyncData = entity.GetSyncData(newEntity: true)
                });

                Console.WriteLine($"[AiManager] Respawned {entity.Name}#{entity.Id}.");
            }
        }
    }
}
