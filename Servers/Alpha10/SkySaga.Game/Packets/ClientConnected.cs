namespace SkySaga.Game.Packets;

public static class ClientConnected
{
    private static readonly Guid WorldUuid = new("6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25");
    private static readonly Guid ServerUuid = new("b3e94c07-2a15-4d68-8f31-7c05a9be4610");

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var sessionTicket = bitStream.ReadString();

        var unknownFieldBuffer = new byte[2];
        bitStream.ReadBits(unknownFieldBuffer, 9, alignBitsToRight: true);

        var gameHost = bitStream.ReadString();

        bitStream.ReadClientUuid(out var clientUuid);
        connection.ClientCharacterUuid = clientUuid;

        Console.WriteLine($"[ClientConnected] sessionTicket={sessionTicket} "
            + $"unknownField9bits=0x{unknownFieldBuffer[0]:X2}{unknownFieldBuffer[1]:X2} "
            + $"gameHost={gameHost} uuid={clientUuid?.ToString() ?? "<none>"}");

        bool attachDebuggerBeforeHandshake = false;
        if (attachDebuggerBeforeHandshake)
        {
            const int debugAttachDelaySeconds = 5;
            Console.WriteLine($"[ClientConnected] Sleeping {debugAttachDelaySeconds}s "
                + "before sending ServerInfo/MapDefinition - attach x32dbg now.");
            Thread.Sleep(debugAttachDelaySeconds * 1000);
            Console.WriteLine("[ClientConnected] Delay done, continuing.");
        }

        Guid? serverOwnerGuid = Guid.TryParse(Util.CharacterUuid(), out var parsedOwnerGuid)
            ? parsedOwnerGuid
            : null;

        var serverInfo = new ServerInfo
        {
            ServerOwnerGuid = serverOwnerGuid,
            WorldUuid = WorldUuid,
            ServerUuid = ServerUuid,

            ServerOwnerName = Util.CharacterName(),

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

        connection.Send(new GetUserToken());

        return true;
    }
}
