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

        Console.WriteLine($"[ClientReadyToPlay] Sent SetClientLocalPlayerEntity(EntityId={connection.PlayerEntity.Id}), "
            + $"PlayerSpawned(Position={playerSpawned.Position}, Yaw={playerSpawned.YawDegrees}, "
            + $"Pitch={playerSpawned.PitchDegrees}).");

        // The Adventures screen is populated by RPCSetMapRotationList (26), not
        // by the world-definition packet.  It must be sent after the client is
        // in the playable state; otherwise the destination UI may not yet have
        // registered its receiver.  The three mastery cards use their genuine
        // Alpha 10 GeoData identifiers, so the client supplies its normal
        // coloured keystone-fragment requirement display instead of FREE.
        // The Mastery cards route to their dedicated test instances. Their
        // original map content is still being recreated separately.
        SendAdventureRotation(connection);

        var entityAdd = new EntityAdd
        {
            Id = connection.PlayerEntity.Id,
            NameHash = Util.ComputeCrc32(connection.PlayerEntity.Name),
            SyncData = connection.PlayerEntity.GetSyncData(newEntity: true)
        };

        connection.PlayerConnectionManager.BroadcastToAllExcept(connection.Guid.g, entityAdd);

        return true;
    }

    /// <summary>
    /// The client evaluates a card's item requirements when its rotation list
    /// arrives. Re-send the list after an inventory transfer so it evaluates
    /// the actual post-transfer stacks rather than keeping stale availability.
    /// </summary>
    public static void SendAdventureRotation(PlayerConnection connection, bool flushPlayerInventory = false)
    {
        // Inventory changes are normally flushed on the next server tick. Send
        // the player's pending sync first, on the same reliable ordered stream,
        // so the client receives the new stacks before it re-evaluates cards.
        if (flushPlayerInventory && connection.PlayerEntity.SyncRequired)
        {
            var inventorySync = connection.PlayerEntity.GetSyncData(newEntity: false, forOwner: true);
            if (inventorySync.GetNumberOfBitsUsed() > 0)
            {
                connection.Send(new EntitySync
                {
                    Id = connection.PlayerEntity.Id,
                    SyncData = inventorySync
                });
            }
        }

        var adventureRotation = new SetMapRotationList
        {
            Specs =
            [
                MapSpec.Ftue(seed: 1337),
                MapSpec.MasteryAdventure(seed: 1338, MapSpec.AdventureBiome.Forest),
                MapSpec.MasteryAdventure(seed: 1339, MapSpec.AdventureBiome.Desert),
                MapSpec.MasteryAdventure(seed: 1340, MapSpec.AdventureBiome.Jungle),
            ],
            Unnamed = 0
        };

        connection.Send(adventureRotation);

        Console.WriteLine("[ClientReadyToPlay] Sent SetMapRotationList: FTUE Old City plus "
            + "Forest, Desert, and Jungle Mastery 01 cards.");
    }
}
