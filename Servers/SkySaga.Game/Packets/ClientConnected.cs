namespace SkySaga.Game.Packets;

public static class ClientConnected
{
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





        const int debugAttachDelaySeconds = 5;
        Console.WriteLine($"[ClientConnected] Sleeping {debugAttachDelaySeconds}s "
            + "before sending ServerInfo/MapDefinition - attach x32dbg now.");
        Thread.Sleep(debugAttachDelaySeconds * 1000);
        Console.WriteLine("[ClientConnected] Delay done, continuing.");

        const string ftueAdventureCrcSeed = "FTUE_The_Old_City"; // подтверждено GeoData.json, Adventures[78], IsFTUE:true

        var serverInfo = new ServerInfo
            {
                ServerOwnerGuid = Util.CharacterUuid(),
                ServerOwnerName = "NeUbivashka",
                ServerBiome = "Forest",
                ServerAdventureCrc = Util.ComputeCrc32(ftueAdventureCrcSeed),
                IsMyWorld = true,
                ChatHost = "127.0.0.1",
                ChatPort = 444
            };

        connection.Send(serverInfo);

        connection.Send(serverInfo);











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




        Console.WriteLine("[ClientConnected] Sending ResourceCheck.");
        connection.Send(new ResourceCheck());





        var setConnectionTimeout = new SetConnectionTimeout { Millis = 30000 };

        connection.Send(setConnectionTimeout);

        Console.WriteLine("[ClientConnected] Sent SetConnectionTimeout: Millis=30000");

        return true;
    }
}