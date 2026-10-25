namespace SkySaga.Game.Packets;

public class JoinSocialHub : ISerializablePacket
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var triggeringEntityId = (uint)connection.PlayerEntity.Id;
        if (bitStream.GetNumberOfUnreadBits() >= 32 &&
            !bitStream.ReadFullWidthUInt32(out triggeringEntityId))
            return false;

        return Transfer(connection, triggeringEntityId);
    }

    internal static bool Transfer(PlayerConnection connection, uint triggeringEntityId)
    {
        var sourceWorld = WorldTransfer.CurrentWorldKind;
        if (sourceWorld is not ("home" or "adventure"))
        {
            Console.WriteLine($"[JoinSocialHub] Ignored request from {connection.Identity.Name} in {sourceWorld}.");
            return true;
        }

        var destinationSpec = MapSpec.SocialHub(seed: 1337);
        if (!MapSpec.TryGetMapSizeWireIndex(4, 4, 4, out var mapSizeIndex))
            throw new InvalidOperationException("The City of Last Light map size has no wire index.");
        destinationSpec.Searchable[MapSpec.MapSizeSlot] = mapSizeIndex;

        return WorldTransfer.Transfer(connection, triggeringEntityId, "SKYSAGA_SOCIAL_HUB_PORT",
            ClientConnected.SocialWorldUuid, ClientConnected.SocialServerUuid, destinationSpec,
            expectedSourceWorld: sourceWorld);
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinSocialHub);

        return bitStream;
    }
}
