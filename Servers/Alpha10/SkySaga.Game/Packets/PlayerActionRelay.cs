namespace SkySaga.Game.Packets;

public static class PlayerActionRelay
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId))
            return false;

        Relay(connection, bitStream, entityId);
        return true;
    }

    public static void Relay(PlayerConnection connection, BitStream bitStream, uint entityId)
    {
        if (connection.IsPlayerInitialized && entityId == (uint)connection.PlayerEntity.Id)
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, bitStream);
    }
}
