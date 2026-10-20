using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public class AiManager
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
    private static readonly uint DefaultPerformerKey = Util.ComputeCrc32("Default Performer");

    private const byte StateIdle = 0;
    private const byte StateRun = 4;
    private const float DirectionScale = 64f;
    private const float InputSpeedScale = 8f;
    private const string MeleeTag = "ActionMelee";
    private const string AlertTag = "EmoteAlert";
    private const string LostTag = "EmoteLost";
    private const string VictoryTag = "EmoteVictory";

    private static readonly Dictionary<int, Brain> Brains = [];

    private readonly IWorldManager _worldManager;
    private readonly PlayerConnectionManager _playerConnectionManager;
    private readonly Random _random = new();

    private DateTime _lastUpdate = DateTime.UtcNow;

    private sealed class Brain
    {
        public required float GroundOffset;
        public int TargetId;
        public AiReaction Mode;
        public DateTime HuntUntil;
        public DateTime NextAttack;
        public Attack? Current;
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
    }

    public static void Hit(Entity creature, Entity attacker)
    {
        if (!Brains.TryGetValue(creature.Id, out var brain) || !CreatureAiTable.TryGet(creature.Name, out var ai))
            return;

        if (ai.OnHit is not (AiReaction.Aggressive or AiReaction.Scared))
            return;

        brain.TargetId = attacker.Id;
        brain.Mode = ai.OnHit;
        brain.HuntUntil = DateTime.UtcNow.AddSeconds(ai.HuntTimeAfterHit);
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

        var players = _playerConnectionManager.GetAllConnections()
            .Select(connection => connection.PlayerEntity)
            .Where(player => player.TryGetComponent<ClientHealthComponent>(out var health) && !health.IsDead && Combat.PositionOf(player) is not null)
            .ToList();

        foreach (var entity in _worldManager.EntityManager.Entities.ToList())
        {
            if (!entity.TryGetComponent<ClientEnemyAiComponent>(out var component)
                || !entity.TryGetComponent<SmoothedTransformComponent>(out var transform)
                || !CreatureAiTable.TryGet(entity.Name, out var ai))
                continue;

            if (!Brains.TryGetValue(entity.Id, out var brain))
                Brains[entity.Id] = brain = new Brain { GroundOffset = transform.Position.Y - GroundTop(transform.Position) };

            if (entity.TryGetComponent<ClientHealthComponent>(out var health) && health.IsDead || Knockback.IsActive(entity.Id))
            {
                brain.Current = null;
                Stop(entity);
                continue;
            }

            Think(entity, component, transform, ai, brain, players, now, delta);
        }
    }

    private void Think(Entity entity, ClientEnemyAiComponent component, TransformComponent transform, CreatureAi ai, Brain brain,
        List<Entity> players, DateTime now, float delta)
    {
        if (brain.Current is Attack attack)
        {
            Stop(entity);

            if (TargetPosition(brain, players) is Vector3 position)
                Face(transform, Horizontal(position - transform.Position));

            if (attack.Ability.HasTag(MeleeTag) && !attack.Resolved && now >= attack.HitAt)
            {
                attack.Resolved = true;

                if (players.FirstOrDefault(player => player.Id == brain.TargetId) is { } victim
                    && Horizontal(Combat.PositionOf(victim)!.Value - transform.Position).Length() <= attack.Ability.Reach)
                    Combat.CreatureAttack(_playerConnectionManager, _worldManager.EntityManager, entity, victim, attack.Ability);
            }

            if (now < attack.EndsAt)
                return;

            brain.Current = null;

            if (attack.Ability.HasTag(MeleeTag))
                brain.NextAttack = now.AddSeconds(ai.NextAttackMinTime + _random.NextDouble() * (ai.NextAttackMaxTime - ai.NextAttackMinTime));
        }

        if (brain.TargetId != 0 && !players.Any(player => player.Id == brain.TargetId)
            && _worldManager.EntityManager.TryGetEntity(brain.TargetId, out var fallen)
            && fallen.TryGetComponent<ClientHealthComponent>(out var fallenHealth) && fallenHealth.IsDead)
        {
            var aggressive = brain.Mode == AiReaction.Aggressive;
            Forget(component, brain);

            if (aggressive && Emote(entity, transform, ai, brain, VictoryTag, 0, now))
                return;
        }

        var hadTarget = brain.TargetId != 0;
        var target = Target(transform, ai, brain, players, now);

        component.TargetEntityId = target?.Id ?? 0;

        if (target is null || Combat.PositionOf(target) is not Vector3 targetPosition)
        {
            Stop(entity);

            if (hadTarget)
                Emote(entity, transform, ai, brain, LostTag, ai.LostTime, now);

            return;
        }

        var offset = Horizontal(targetPosition - transform.Position);
        var distance = offset.Length();
        var toTarget = distance > 0.0001f ? offset / distance : Forward(transform.YawDegrees);

        if (!hadTarget && brain.Mode == AiReaction.Aggressive)
        {
            Face(transform, toTarget);

            if (Emote(entity, transform, ai, brain, AlertTag, ai.AlertTime, now))
                return;
        }

        if (brain.Mode == AiReaction.Scared)
        {
            Move(entity, transform, ai, brain, -toTarget, ai.RunSpeed, delta);
            return;
        }

        var melee = ai.Tagged(MeleeTag).ToList();

        if (melee.Count == 0)
        {
            Stop(entity);
            Face(transform, toTarget);
            return;
        }

        var inRange = melee.Where(ability => distance <= ability.Reach).ToList();

        if (inRange.Count == 0)
        {
            Move(entity, transform, ai, brain, toTarget, ai.RunSpeed, delta);
            return;
        }

        Stop(entity);
        Face(transform, toTarget);

        if (now >= brain.NextAttack)
            Perform(entity, transform, brain, inRange[_random.Next(inRange.Count)], toTarget, 0, now);
    }

    private bool Emote(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, string tag, float time, DateTime now)
    {
        var emotes = ai.Tagged(tag).ToList();

        if (emotes.Count == 0)
            return false;

        Perform(entity, transform, brain, emotes[_random.Next(emotes.Count)], Forward(transform.YawDegrees), time, now);
        return true;
    }

    private static void Forget(ClientEnemyAiComponent component, Brain brain)
    {
        brain.TargetId = 0;
        brain.Mode = AiReaction.Ignore;
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
                && (now < brain.HuntUntil || Vector3.Distance(position, transform.Position) <= ai.SeenDistance))
                return current;

            brain.TargetId = 0;
            brain.Mode = AiReaction.Ignore;
            return null;
        }

        foreach (var player in players)
        {
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

    private void Move(Entity entity, TransformComponent transform, CreatureAi ai, Brain brain, Vector3 direction, float speed, float delta)
    {
        Face(transform, direction);

        var next = transform.Position + direction * speed * delta;
        var ground = GroundTop(next);

        if (ground - GroundTop(transform.Position) > ai.JumpHeight)
        {
            Stop(entity);
            return;
        }

        transform.Position = new Vector3(next.X, ground + brain.GroundOffset, next.Z);

        SetMovement(entity, direction, speed, StateRun);
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

    private float GroundTop(Vector3 position)
    {
        var mapHeightVoxels = Math.Max(_worldManager.Definition.MapSizeChunks.Y, 1) * 32;

        return SpawnFinder.FindGroundHeight(_worldManager.ChunkManager, (int)MathF.Floor(position.X), (int)MathF.Floor(position.Z), mapHeightVoxels) + 1;
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
