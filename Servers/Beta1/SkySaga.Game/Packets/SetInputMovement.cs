namespace SkySaga.Game.Packets;

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
