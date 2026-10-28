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
            playerSpawned.YawDegrees = connection.LookYaw;
            playerSpawned.PitchDegrees = connection.LookPitch;
        }

        connection.Send(playerSpawned);
        connection.Ai.PlayerSpawned(connection.PlayerEntity, respawn: false);

        Console.WriteLine($"[ClientReadyToPlay] Sent SetClientLocalPlayerEntity(EntityId={connection.PlayerEntity.Id}), "
            + $"PlayerSpawned(Position={playerSpawned.Position}, Yaw={playerSpawned.YawDegrees}, "
            + $"Pitch={playerSpawned.PitchDegrees}).");

        foreach (var entity in connection.WorldManager.InventoryFirst([connection.PlayerEntity]))
        {
            connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, new EntityAdd
            {
                Id = entity.Id,
                NameHash = Util.ComputeCrc32(entity.Name),
                SyncData = entity.GetSyncData(newEntity: true, includeOwnerOnly: false)
            });
        }

        return true;
    }
}
