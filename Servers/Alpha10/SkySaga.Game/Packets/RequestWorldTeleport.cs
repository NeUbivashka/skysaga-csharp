namespace SkySaga.Game.Packets;

public class RequestWorldTeleport : ISerializablePacket
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        // Alpha 10 sends the selected world as an optional client UUID,
        // followed by the player entity which initiated the request.  The
        // remaining bits are byte-alignment padding.
        if (!bitStream.ReadClientUuid(out var requestedWorld)
            || requestedWorld is not Guid worldUuid
            || !bitStream.ReadFullWidthUInt32(out var triggeringEntityId))
            return false;

        // The City is a fixed server, not a player-owned world-island record.
        // Adventure instances route it through their configured City listener.
        if (worldUuid == ClientConnected.SocialWorldUuid)
        {
            Console.WriteLine($"[RequestWorldTeleport] {connection.Identity.Name} selected the City from entity {triggeringEntityId}.");
            return JoinSocialHub.Transfer(connection, triggeringEntityId);
        }

        if (!Util.TryResolveWorld(worldUuid, out var destination))
        {
            Console.WriteLine($"[RequestWorldTeleport] World {worldUuid:D} is unavailable for "
                + $"{connection.Identity.Name}.");
            return true;
        }

        Console.WriteLine($"[RequestWorldTeleport] {connection.Identity.Name} selected world "
            + $"{worldUuid:D} from entity {triggeringEntityId}.");
        var currentWorld = WorldTransfer.CurrentWorldKind;
        return WorldTransfer.Transfer(connection, triggeringEntityId, destination.Port,
            destination.WorldUuid, destination.ServerUuid, MapSpec.HomeIsland(seed: 1337),
            expectedSourceWorld: currentWorld);
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestWorldTeleport);

        return bitStream;
    }
}
