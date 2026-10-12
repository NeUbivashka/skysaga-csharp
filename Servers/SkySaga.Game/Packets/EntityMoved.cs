namespace SkySaga.Game.Packets;

public static class EntityMoved
{
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
        }

        return true;
    }
}
