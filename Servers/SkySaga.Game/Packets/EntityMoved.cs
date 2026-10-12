namespace SkySaga.Game.Packets;

/// <summary>
/// RPCEntityMoved, ordinal 155, client to server (send stub FUN_00B2A460). The owning client reports
/// its character's position, 64 bits, no entity id:
///
///   X     ranged max 0x10000, 1/64 voxel                 17 bits   (FUN_00859080)
///   Y     ranged max 0x7F80 of (y + 0x3FC0), 1/64 voxel  15 bits
///   Z     ranged max 0x10000, 1/64 voxel                 17 bits
///   yaw   ranged max 0x6400 of (yaw + 0x3200), 1/32 deg  15 bits   (FUN_00859000)
///
/// This used to read a 32-bit entity id first and treated every Y as unsigned, so it failed on
/// every packet and the server never saw the player move.
/// </summary>
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
