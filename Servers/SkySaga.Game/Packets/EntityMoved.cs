namespace SkySaga.Game.Packets;

public static class EntityMoved
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.Read(out int entityId))
            return false;

        if (!ReadPosition(bitStream, out var positionX)) return false;
        if (!ReadPosition(bitStream, out var positionY)) return false;
        if (!ReadPosition(bitStream, out var positionZ)) return false;

        if (!ReadYaw(bitStream, out var yaw)) return false;

        if (connection.PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var smoothedTransformComponent))
        {
            smoothedTransformComponent.Position = new Vector3(positionX, positionY, positionZ);
            smoothedTransformComponent.YawDegrees = yaw;
        }

        return true;
    }

    private static bool ReadPosition(BitStream bitStream, out float position)
    {
        if (!bitStream.ReadInt32(0x10000, out var tmpPosition))
        {
            position = 0;
            return false;
        }
        position = tmpPosition / 64f;
        return true;
    }

    private static bool ReadYaw(BitStream bitStream, out float yaw)
    {
        if (!bitStream.ReadInt32(25600, out var tmpYaw))
        {
            yaw = 0;
            return false;
        }
        yaw = (tmpYaw - 0x3200) * 0.03125f;
        return true;
    }
}