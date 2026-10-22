namespace SkySaga.Game.Packets;

internal static class WorldTransfer
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream, string destinationPortVariable,
        Guid destinationWorld, Guid destinationServer, MapSpec destinationSpec, string expectedSourceWorld)
    {
        if (!ushort.TryParse(Environment.GetEnvironmentVariable(destinationPortVariable), out var port) ||
            port == 0)
        {
            Console.WriteLine($"[WorldTransfer] Destination unavailable: {destinationPortVariable} is unset.");
            return true;
        }

        return Handle(connection, bitStream, port, destinationWorld, destinationServer, destinationSpec,
            expectedSourceWorld);
    }

    public static bool Handle(PlayerConnection connection, BitStream bitStream, ushort destinationPort,
        Guid destinationWorld, Guid destinationServer, MapSpec destinationSpec, string expectedSourceWorld)
    {
        if (!bitStream.ReadFullWidthUInt32(out var triggeringEntityId))
            return false;

        return Transfer(connection, triggeringEntityId, destinationPort, destinationWorld, destinationServer,
            destinationSpec, expectedSourceWorld);
    }

    public static bool Transfer(PlayerConnection connection, uint triggeringEntityId, ushort destinationPort,
        Guid destinationWorld, Guid destinationServer, MapSpec destinationSpec, string expectedSourceWorld)
    {
        var currentWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social" ? "social" : "home";
        if (currentWorld != expectedSourceWorld || !connection.IsPlayerInitialized ||
            triggeringEntityId != (uint)connection.PlayerEntity.Id)
        {
            Console.WriteLine($"[WorldTransfer] Ignored request from entity {triggeringEntityId} in {currentWorld}.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(connection.GameHost))
        {
            Console.WriteLine("[WorldTransfer] Destination unavailable: game host is unset.");
            return true;
        }

        connection.SaveState();
        connection.Send(new TransferToServer
        {
            WorldUuid = destinationWorld,
            ServerUuid = destinationServer,
            Host = connection.GameHost,
            Port = destinationPort,
            // The initial, working game-conductor connection has no transport
            // token. SessionTicket is a separate account credential.
            Token = []
        });

        // The client stores TransferToServer's endpoint, then waits for its
        // gameplay unload path before LoadWorld uses that endpoint.
        connection.Send(new TeleportBegins { Spec = destinationSpec });

        Console.WriteLine($"[WorldTransfer] Requested transfer for entity {triggeringEntityId} to {connection.GameHost}:{destinationPort} world={destinationWorld}; sent TeleportBegins.");
        return true;
    }
}
