namespace SkySaga.Game.Packets;

public static class ClientConnected
{
    /// <summary>
    /// Stable per-server identities. The client stores WorldUUID and ServerUUID and hands them
    /// back later, so they have to be the same value for the whole run rather than fresh Guids
    /// on every connection.
    /// </summary>
    private static readonly Guid WorldUuid = new("6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25");
    private static readonly Guid ServerUuid = new("b3e94c07-2a15-4d68-8f31-7c05a9be4610");

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var sessionTicket = bitStream.ReadString();

        var unknownFieldBuffer = new byte[2];
        bitStream.ReadBits(unknownFieldBuffer, 9, alignBitsToRight: true);

        var gameHost = bitStream.ReadString();

        var trailingBitBuffer = new byte[1];
        bitStream.ReadBits(trailingBitBuffer, 1, alignBitsToRight: true);

        Console.WriteLine($"[ClientConnected] sessionTicket={sessionTicket} "
            + $"unknownField9bits=0x{unknownFieldBuffer[0]:X2}{unknownFieldBuffer[1]:X2} "
            + $"gameHost={gameHost} trailingBit=0x{trailingBitBuffer[0]:X2}");

        // Debug-attach stall: was hardcoded on for every connection (blocks real play testing).
        // Flip to true only when you actually need the x32dbg-attach window.
        // Not `const` on purpose - a const bool makes the compiler treat this whole `if` as
        // unreachable at compile time and emit CS0162 on the block's contents.
        bool attachDebuggerBeforeHandshake = false;
        if (attachDebuggerBeforeHandshake)
        {
            const int debugAttachDelaySeconds = 5;
            Console.WriteLine($"[ClientConnected] Sleeping {debugAttachDelaySeconds}s "
                + "before sending ServerInfo/MapDefinition - attach x32dbg now.");
            Thread.Sleep(debugAttachDelaySeconds * 1000);
            Console.WriteLine("[ClientConnected] Delay done, continuing.");
        }

        // Util.CharacterUuid() hands back a string from the web service, which is not guaranteed
        // to parse. A null ServerOwnerGUID is a legal value on the wire (presence bit 0), so fall
        // back to that rather than inventing a Guid the web service does not know about.
        Guid? serverOwnerGuid = Guid.TryParse(Util.CharacterUuid(), out var parsedOwnerGuid)
            ? parsedOwnerGuid
            : null;

        var serverInfo = new ServerInfo
        {
            ServerOwnerGuid = serverOwnerGuid,
            WorldUuid = WorldUuid,
            ServerUuid = ServerUuid,

            ServerOwnerName = "NeUbivashka",

            ChatHost = "127.0.0.1",
            ChatPort = 444,

            // Client clamps both to 32.
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

        // ResourceCheck (ordinal 10) used to go out here. It does not belong on this side of the
        // wire: the client has no receive stub for ordinal 10 at all, only a sender
        // (FUN_008260D0, which opens an outgoing RPC via FUN_00BC56B0(10, ...) and writes a
        // length plus a byte array). Anything the server sent under that ordinal was dropped by
        // the dispatcher without a word. If ResourceCheck is needed, it is the client that asks
        // and the server that answers - it belongs in PlayerConnection.ProcessPacket.

        var setConnectionTimeout = new SetConnectionTimeout { Millis = 30000 };

        connection.Send(setConnectionTimeout);

        Console.WriteLine("[ClientConnected] Sent SetConnectionTimeout: Millis=30000");

        return true;
    }
}
