namespace SkySaga.Game.Packets;

public static class ClientReadyToPlay
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientReadyToPlay));



        connection.Send(new SetClientEntity { EntityId = connection.PlayerEntity.Id });
        connection.Send(new UnlockPosition());
        connection.Send(new PlayerSpawned());

        var entityAdd = new EntityAdd
        {
            Id = connection.PlayerEntity.Id,
            NameHash = Util.ComputeCrc32(connection.PlayerEntity.Name),
            SyncData = connection.PlayerEntity.GetSyncData(newEntity: true)
        };

        connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, entityAdd);

        return true;
    }
}