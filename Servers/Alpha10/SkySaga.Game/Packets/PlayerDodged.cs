namespace SkySaga.Game.Packets;

public static class PlayerDodged
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(8, out var value)) return false;

        Console.WriteLine($"[PlayerDodged] value={value}");

        connection.PlayerConnectionManager.BroadcastToAllExcept(
            connection.Guid.g,
            new EntityDodged
            {
                EntityId = connection.PlayerEntity.Id,
                DodgeKind = (uint)value
            });

        return true;
    }
}
