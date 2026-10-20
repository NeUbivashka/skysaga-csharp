using System.Linq;

namespace SkySaga.Game.Managers.Entities;

public static class Knockback
{
    private const float MaxKnockbackStrength = 100f;
    private const float CurveFactor = 1f;
    private const float Gravity = 20f;

    private sealed class Flight
    {
        public required Vector3 Start;
        public required Vector3 Velocity;
        public required DateTime Started;
    }

    private static readonly Dictionary<int, Flight> Active = [];

    public static float Distance(float distance, float strength, float resistance)
    {
        var share = Math.Clamp((strength - resistance) / MaxKnockbackStrength, 0f, 1f);
        return distance * MathF.Pow(share, CurveFactor);
    }

    public static bool Apply(Entity target, Vector3 attackerPosition, float attackerYawDegrees, float distance, float pitchDegrees, float yawOffsetDegrees)
    {
        if (distance <= 0 || pitchDegrees <= 0 || pitchDegrees >= 90 || !target.TryGetComponent<SmoothedTransformComponent>(out var transform))
            return false;

        var away = transform.Position - attackerPosition;
        away.Y = 0f;

        var yaw = (away.LengthSquared() > 0.0001f ? MathF.Atan2(away.X, away.Z) : attackerYawDegrees * MathF.PI / 180f)
            + yawOffsetDegrees * MathF.PI / 180f;
        var pitch = pitchDegrees * MathF.PI / 180f;
        var speed = MathF.Sqrt(distance * Gravity / MathF.Sin(2 * pitch));

        Active[target.Id] = new Flight
        {
            Start = transform.Position,
            Velocity = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch)) * speed,
            Started = DateTime.UtcNow
        };

        return true;
    }

    public static bool IsActive(int entityId) => Active.ContainsKey(entityId);

    public static void Cancel(int entityId) => Active.Remove(entityId);

    public static void Update(IWorldManager worldManager, DateTime now)
    {
        foreach (var (entityId, flight) in Active.ToList())
        {
            if (!worldManager.EntityManager.TryGetEntity(entityId, out var entity)
                || !entity.TryGetComponent<SmoothedTransformComponent>(out var transform))
            {
                Active.Remove(entityId);
                continue;
            }

            var time = (float)(now - flight.Started).TotalSeconds;
            var position = flight.Start + flight.Velocity * time + new Vector3(0f, -Gravity * time * time / 2f, 0f);
            var horizontal = new Vector3(position.X, transform.Position.Y, position.Z);

            if (SpawnFinder.IsSolidAt(worldManager.ChunkManager, horizontal))
            {
                Land(worldManager, entityId, transform, transform.Position);
                continue;
            }

            var ground = GroundY(worldManager, position);

            if (ground is null && position.Y < 0f)
            {
                Active.Remove(entityId);
                continue;
            }

            if (flight.Velocity.Y - Gravity * time <= 0f && ground is float groundY && position.Y <= groundY)
            {
                Land(worldManager, entityId, transform, position);
                continue;
            }

            transform.Position = position;
        }
    }

    private static void Land(IWorldManager worldManager, int entityId, TransformComponent transform, Vector3 position)
    {
        Active.Remove(entityId);
        transform.Position = new Vector3(position.X, GroundY(worldManager, position) ?? position.Y, position.Z);
    }

    private static float? GroundY(IWorldManager worldManager, Vector3 position)
        => SpawnFinder.GroundBelow(worldManager.ChunkManager, position, 0f);
}
