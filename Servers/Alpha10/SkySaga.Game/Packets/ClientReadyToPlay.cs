namespace SkySaga.Game.Packets;

public static class ClientReadyToPlay
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientReadyToPlay));

        connection.Send(new SetClientEntity { EntityId = connection.PlayerEntity.Id });

        var playerSpawned = new PlayerSpawned
        {
            EntityId = connection.PlayerEntity.Id
        };

        if (connection.PlayerEntity.TryGetComponent<SmoothedTransformComponent>(out var transform))
        {
            playerSpawned.Position = transform.Position;
            playerSpawned.YawDegrees = transform.YawDegrees;
        }

        connection.Send(playerSpawned);

        Console.WriteLine($"[ClientReadyToPlay] Sent SetClientLocalPlayerEntity(EntityId={connection.PlayerEntity.Id}), "
            + $"PlayerSpawned(Position={playerSpawned.Position}, Yaw={playerSpawned.YawDegrees}, "
            + $"Pitch={playerSpawned.PitchDegrees}).");

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
