namespace SkySaga.Game.Packets;

/// <summary>
/// RPCSetInputMovement, ordinal 76, client to server (send stub FUN_00B24140). The movement input
/// of the player's character, sent whenever it changes:
///
///   direction   3 x ranged max 0x80 of (component + 0x40)   3 x 8 bits  (FUN_008621B0)
///   speed       ranged max 0x78                              7 bits      (FUN_00B23870)
///
/// Idle is "40 40 40 00" (zero vector, speed 0). The server stores it on
/// ClientCharacterPhysicsComponent, whose inputmovementdirection / inputmovementspeed bindings sync
/// it back to every client, the owner included.
/// </summary>
public static class SetInputMovement
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientCharacterPhysicsComponent.DirectionMax, out var x)) return false;
        if (!bitStream.ReadInt32(ClientCharacterPhysicsComponent.DirectionMax, out var y)) return false;
        if (!bitStream.ReadInt32(ClientCharacterPhysicsComponent.DirectionMax, out var z)) return false;
        if (!bitStream.ReadInt32(ClientCharacterPhysicsComponent.SpeedMax, out var speed)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientCharacterPhysicsComponent>(out var physics))
        {
            physics.InputMovementDirection = (x - 0x40, y - 0x40, z - 0x40);
            physics.InputMovementSpeed = (uint)speed;
        }

        return true;
    }
}
