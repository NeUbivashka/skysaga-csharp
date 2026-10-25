namespace SkySaga.Game.Packets;

public static class ClientConnected
{
    internal static readonly Guid HomeWorldUuid = new("6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25");
    internal static readonly Guid HomeServerUuid = new("b3e94c07-2a15-4d68-8f31-7c05a9be4610");
    internal static readonly Guid SocialWorldUuid = new("b6574a23-64df-4c26-9c31-40b2a485d0fe");
    internal static readonly Guid SocialServerUuid = new("451c4d0c-43c5-4c72-b59c-b039b9513bc0");

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var sessionTicket = bitStream.ReadString();

        var unknownFieldBuffer = new byte[2];
        bitStream.ReadBits(unknownFieldBuffer, 9, alignBitsToRight: true);

        var gameHost = bitStream.ReadString();

        connection.SessionTicket = sessionTicket;
        connection.GameHost = gameHost;

        var trailingBitBuffer = new byte[1];
        bitStream.ReadBits(trailingBitBuffer, 1, alignBitsToRight: true);

        Console.WriteLine($"[ClientConnected] sessionTicket={sessionTicket} "
            + $"unknownField9bits=0x{unknownFieldBuffer[0]:X2}{unknownFieldBuffer[1]:X2} "
            + $"gameHost={gameHost} trailingBit=0x{trailingBitBuffer[0]:X2}");

        bool attachDebuggerBeforeHandshake = false;
        if (attachDebuggerBeforeHandshake)
        {
            const int debugAttachDelaySeconds = 5;
            Console.WriteLine($"[ClientConnected] Sleeping {debugAttachDelaySeconds}s "
                + "before sending ServerInfo/MapDefinition - attach x32dbg now.");
            Thread.Sleep(debugAttachDelaySeconds * 1000);
            Console.WriteLine("[ClientConnected] Delay done, continuing.");
        }

        var isSocialWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";
        var isAdventureWorld = WorldTransfer.CurrentWorldKind == "adventure";
        var adventure = RequestAdventureStart.CurrentDestination();
        // Neither social hubs nor adventures are character-owned islands.
        // Sending the local player's UUID here makes the client label either
        // one as that player's "Ruins", regardless of ServerOwnerName.
        Guid? serverOwnerGuid = isSocialWorld || isAdventureWorld
            ? null
            : Guid.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_HOME_OWNER_UUID"), out var islandOwner)
                ? islandOwner
                : Guid.TryParse(Util.CharacterUuid(), out var parsedOwnerGuid) ? parsedOwnerGuid : null;

        var worldUuid = adventure?.WorldUuid ?? (isSocialWorld ? SocialWorldUuid : HomeWorldUuid);
        var serverUuid = adventure?.ServerUuid ?? (isSocialWorld ? SocialServerUuid : HomeServerUuid);
        if (!isSocialWorld && !isAdventureWorld && Guid.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_HOME_WORLD_UUID"), out var islandWorld))
            worldUuid = islandWorld;
        if (!isSocialWorld && !isAdventureWorld && Guid.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_HOME_SERVER_UUID"), out var islandServer))
            serverUuid = islandServer;

        var serverInfo = new ServerInfo
        {
            ServerOwnerGuid = serverOwnerGuid,
            WorldUuid = worldUuid,
            ServerUuid = serverUuid,

            ServerOwnerName = isAdventureWorld
                ? adventure?.DisplayName ?? "Adventure"
                : !isSocialWorld
                ? Environment.GetEnvironmentVariable("SKYSAGA_HOME_OWNER_NAME") ?? Util.CharacterName()
                : "The City of Last Light",

            ChatHost = "127.0.0.1",
            ChatPort = 444,

            MaxUsers = 32,
            MinUsersRequiredToPlay = 1,

            GameModeEntityId = 0,

            IsOpenedToMatchmakingForMorePlayers = false
        };

        var serverInfoStream = serverInfo.Serialize();

        Console.WriteLine($"[ClientConnected] Sending ServerInfo: "
            + $"ServerOwnerGUID={serverInfo.ServerOwnerGuid?.ToString() ?? "<none>"} "
            + $"WorldUUID={serverInfo.WorldUuid} ServerUUID={serverInfo.ServerUuid} "
            + $"ServerOwnerName='{serverInfo.ServerOwnerName}' "
            + $"ChatHost='{serverInfo.ChatHost}' ChatPort={serverInfo.ChatPort} "
            + $"MaxUsers={serverInfo.MaxUsers} MinUsers={serverInfo.MinUsersRequiredToPlay} "
            + $"GameModeEntityID={serverInfo.GameModeEntityId} "
            + $"({serverInfoStream.GetNumberOfBytesUsed()} bytes, "
            + $"{serverInfoStream.GetNumberOfBitsUsed()} bits used)");

        connection.Send(serverInfoStream);

        var definition = connection.WorldManager.Definition;

        Console.WriteLine($"[ClientConnected] Sending MapDefinition: "
            + $"Searchable=[{string.Join(",", definition.Spec.Searchable)}] "
            + $"AdventureType='{definition.AdventureType}' MapFileName='{definition.MapFileName}' "
            + $"FeatureName='{definition.Spec.FeatureName}' Seed={definition.Spec.Seed} "
            + $"GameMode={definition.GameMode}");

        var mapDefinitionStream = definition.Serialize();
        mapDefinitionStream.PrintHex(out var mapDefinitionHex);
        Console.WriteLine($"[ClientConnected] MapDefinition raw bytes "
            + $"({mapDefinitionStream.GetNumberOfBytesUsed()} bytes, "
            + $"{mapDefinitionStream.GetNumberOfBitsUsed()} bits used): {mapDefinitionHex}");

        connection.Send(mapDefinitionStream);

        var setConnectionTimeout = new SetConnectionTimeout { Millis = 30000 };

        connection.Send(setConnectionTimeout);

        Console.WriteLine("[ClientConnected] Sent SetConnectionTimeout: Millis=30000");

        return true;
    }
}
