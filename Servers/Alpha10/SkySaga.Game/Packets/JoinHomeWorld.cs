namespace SkySaga.Game.Packets;

public class JoinHomeWorld : ISerializablePacket
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        // The in-world portal includes the triggering entity.  The map menu can
        // issue the same request without a payload, in which case the connection
        // itself is the authority for which player and home island to use.
        var triggeringEntityId = (uint)connection.PlayerEntity.Id;
        if (bitStream.GetNumberOfUnreadBits() >= 32 &&
            !bitStream.ReadFullWidthUInt32(out triggeringEntityId))
            return false;

        return Transfer(connection, triggeringEntityId);
    }

    public static bool Transfer(PlayerConnection connection, uint triggeringEntityId)
    {
        if (!Util.TryResolveHomeWorld(connection.Identity.CharacterUuid, out var destination))
        {
            Console.WriteLine($"[JoinHomeWorld] Home island unavailable for {connection.Identity.CharacterUuid:D}.");
            return true;
        }

        Console.WriteLine($"[JoinHomeWorld] Resolved {connection.Identity.Name}'s home island "
            + $"as {destination.WorldUuid:D} on UDP {destination.Port}.");
        return WorldTransfer.Transfer(connection, triggeringEntityId, destination.Port,
            destination.WorldUuid, destination.ServerUuid, MapSpec.HomeIsland(seed: 1337),
            expectedSourceWorld: "social");
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinHomeWorld);

        return bitStream;
    }
}
