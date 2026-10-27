namespace SkySaga.Game.Packets;

public static class ClientConnected
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var sessionTicket = bitStream.ReadString();

        var unknownFieldBuffer = new byte[2];
        bitStream.ReadBits(unknownFieldBuffer, 9, alignBitsToRight: true);

        var gameHost = bitStream.ReadString();

        bitStream.ReadClientUuid(out var clientUuid);
        connection.ClientCharacterUuid = clientUuid;
        connection.GameHost = gameHost;

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

        if (clientUuid is not { } characterId || connection.Server.Worlds.EntryFor(characterId) is not { } entry)
        {
            Console.WriteLine($"[ClientConnected] No character {clientUuid?.ToString() ?? "<none>"}; sending QuitGame.");
            connection.Send(new QuitGame());
            return true;
        }

        var world = connection.Server.Worlds.Open(entry);
        connection.Join(world);

        Console.WriteLine($"[ClientConnected] Character {characterId} enters {entry.Kind} {entry.Id}.");

        var serverInfo = new ServerInfo
        {
            ServerOwnerGuid = entry.OwnerCharacterId,
            WorldUuid = entry.Id,
            ServerUuid = connection.Server.Id,

            ServerOwnerName = entry.OwnerName ?? string.Empty,

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
