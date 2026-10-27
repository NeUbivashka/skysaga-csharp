namespace SkySaga.Game.Packets;

public static class PerformJump
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (connection.IsPlayerInitialized)
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, new EntityJumped { EntityId = connection.PlayerEntity.Id });

        return true;
    }
}
