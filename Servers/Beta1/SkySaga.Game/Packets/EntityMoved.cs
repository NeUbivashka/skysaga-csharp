namespace SkySaga.Game.Packets;

public static class EntityMoved
{
    private const float FallLimitY = -16f;
    private static readonly TimeSpan FallRespawnCooldown = TimeSpan.FromSeconds(3);

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(0x10000, out var x)) return false;
        if (!bitStream.ReadInt32(0x7F80, out var y)) return false;
        if (!bitStream.ReadInt32(0x10000, out var z)) return false;
        if (!bitStream.ReadInt32(0x6400, out var yaw)) return false;

        if (connection.PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransformComponent))
        {
            smoothedTransformComponent.Position = new Vector3(x / 64f, (y - 0x3FC0) / 64f, z / 64f);
            smoothedTransformComponent.YawDegrees = (yaw - 0x3200) * 0.03125f;

            if (smoothedTransformComponent.Position.Y < FallLimitY)
                RespawnAfterFall(connection, smoothedTransformComponent);
        }

        return true;
    }

    private static void RespawnAfterFall(PlayerConnection connection, SmoothedTransformComponent transform)
    {
        var now = DateTime.UtcNow;

        if (now - connection.LastFallRespawn < FallRespawnCooldown)
            return;

        connection.LastFallRespawn = now;

        var fellFrom = transform.Position;
        var (position, yawDegrees) = connection.WorldManager.GetPlayerLandingPoint();

        connection.Teleport(position, yawDegrees);

        Console.WriteLine($"[Fall] player fell to {fellFrom}, sent PlayerSpawned at the airship, {position}");
    }
}
