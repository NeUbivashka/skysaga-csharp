using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public class Projectiles(IWorldManager worldManager, PlayerConnectionManager playerConnectionManager)
{
    private const float WorldGravity = 20f;
    private const float StepSeconds = 0.02f;
    private const float PlayerHalfWidth = 0.5f;
    private const float PlayerHeight = 2f;

    private readonly List<Flight> _flights = [];
    private readonly Random _random = new();

    private sealed class Flight
    {
        public required Entity Projectile;
        public required Entity Owner;
        public required CreatureAbility Ability;
        public required ProjectileLaunch Launch;
        public required Vector3 Position;
        public required Vector3 Velocity;
        public required DateTime Expires;
    }

    public void Launch(Entity owner, Vector3 from, float yawDegrees, Vector3 target, CreatureAbility ability, ProjectileLaunch launch, DateTime now)
    {
        if (!worldManager.EntityManager.TryCreateEntity(launch.Entity, out var projectile))
            return;

        var yaw = yawDegrees * MathF.PI / 180f;
        var right = new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var start = from + right * launch.Offset.X + Vector3.UnitY * launch.Offset.Y + forward * launch.Offset.Z;

        var direction = Vector3.Normalize(target - start);
        direction = Rotate(direction, launch.YawOffset, launch.PitchOffset);
        direction = Scatter(direction, launch.MinInaccuracy + (float)_random.NextDouble() * (launch.MaxInaccuracy - launch.MinInaccuracy));

        var velocity = direction * launch.Speed;

        if (projectile.TryGetComponent<MovingTransformComponent>(out var transform))
        {
            transform.Position = start;
            transform.Velocity = velocity;
        }

        var itemCrc = Util.ComputeCrc32(launch.Entity);

        if (projectile.TryGetComponent<InventoryItemComponent>(out var item))
        {
            item.InventorySlotData.Name = itemCrc;
            item.InventorySlotData.Count = 1;
        }

        if (MaterialTable.TryGet(itemCrc, out var materials) && projectile.TryGetComponent<MaterialCompositionComponent>(out var composition))
            composition.MaterialList = materials;

        if (projectile.TryGetComponent<ClientProjectileComponent>(out var component))
        {
            component.ProjectileDataCount = launch.DataCount;
            component.ProjectileDataIndex = launch.DataIndex;
            component.Gravity = launch.GravityFactor;
            component.OwningEntityId = owner.Id;
            component.LauncherEntityId = owner.Id;
        }

        playerConnectionManager.BroadcastToAll(new EntityAdd
        {
            Id = projectile.Id,
            NameHash = Util.ComputeCrc32(projectile.Name),
            SyncData = projectile.GetSyncData(newEntity: true)
        });

        _flights.Add(new Flight
        {
            Projectile = projectile,
            Owner = owner,
            Ability = ability,
            Launch = launch,
            Position = start,
            Velocity = velocity,
            Expires = now.AddSeconds(launch.Lifetime)
        });

        Console.WriteLine($"[Projectiles] {owner.Name}#{owner.Id} launched {projectile.Name}#{projectile.Id} at {velocity.Length():0.#} m/s");
    }

    public void Update(DateTime now, float delta)
    {
        if (_flights.Count == 0)
            return;

        var players = playerConnectionManager.GetAllConnections()
            .Select(connection => connection.PlayerEntity)
            .Where(player => player.TryGetComponent<ClientHealthComponent>(out var health) && !health.IsDead && Combat.PositionOf(player) is not null)
            .ToList();

        foreach (var flight in _flights.ToList())
        {
            if (now >= flight.Expires)
            {
                Remove(flight, impact: null);
                continue;
            }

            for (var elapsed = 0f; elapsed < delta; elapsed += StepSeconds)
            {
                var step = MathF.Min(StepSeconds, delta - elapsed);

                flight.Velocity += Vector3.UnitY * -WorldGravity * flight.Launch.GravityFactor * step;
                flight.Position += flight.Velocity * step;

                if (players.FirstOrDefault(player => Inside(flight.Position, Combat.PositionOf(player)!.Value)) is { } victim)
                {
                    Combat.CreatureProjectileHit(playerConnectionManager, worldManager.EntityManager, flight.Owner, victim, flight.Ability, flight.Launch);
                    Remove(flight, flight.Position);
                    break;
                }

                if (SpawnFinder.IsSolidAt(worldManager.ChunkManager, flight.Position) || flight.Position.Y < 0f)
                {
                    Remove(flight, flight.Position);
                    break;
                }
            }
        }
    }

    private static bool Inside(Vector3 point, Vector3 player)
        => MathF.Abs(point.X - player.X) <= PlayerHalfWidth
           && MathF.Abs(point.Z - player.Z) <= PlayerHalfWidth
           && point.Y >= player.Y && point.Y <= player.Y + PlayerHeight;

    private void Remove(Flight flight, Vector3? impact)
    {
        _flights.Remove(flight);

        if (impact is Vector3 position)
            playerConnectionManager.BroadcastToAll(new ProjectileImpacted { ProjectileId = flight.Projectile.Id, Position = position });

        worldManager.EntityManager.RemoveEntity(flight.Projectile);
        playerConnectionManager.BroadcastToAll(new EntityRemoved { Id = flight.Projectile.Id });
    }

    private static Vector3 Rotate(Vector3 direction, float yawDegrees, float pitchDegrees)
    {
        var horizontal = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        var yaw = MathF.Atan2(direction.X, direction.Z) + yawDegrees * MathF.PI / 180f;
        var pitch = MathF.Atan2(direction.Y, horizontal) + pitchDegrees * MathF.PI / 180f;

        return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
    }

    private Vector3 Scatter(Vector3 direction, float angleDegrees)
    {
        if (angleDegrees <= 0f)
            return direction;

        var helper = MathF.Abs(direction.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var side = Vector3.Normalize(Vector3.Cross(direction, helper));
        var up = Vector3.Cross(side, direction);
        var roll = (float)(_random.NextDouble() * Math.PI * 2);
        var offset = side * MathF.Cos(roll) + up * MathF.Sin(roll);
        var angle = angleDegrees * MathF.PI / 180f;

        return Vector3.Normalize(direction * MathF.Cos(angle) + offset * MathF.Sin(angle));
    }
}
