namespace SkySaga.Game.Packets;

public static class EndInteractionWithEntity
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var targetEntityId)) return false;

        Console.WriteLine($"[Interaction] entity {entityId} ended interaction with {targetEntityId}");

        if (entityId == connection.PlayerEntity.Id
            && connection.PlayerEntity.TryGetComponent<ClientUseEntityComponent>(out var useEntity)
            && useEntity.UsingEntityId == (int)targetEntityId)
            useEntity.UsingEntityId = 0;

        return true;
    }
}
