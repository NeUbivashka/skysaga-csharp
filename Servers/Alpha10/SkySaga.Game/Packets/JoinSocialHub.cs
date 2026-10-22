namespace SkySaga.Game.Packets;

public class JoinSocialHub : ISerializablePacket
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var destinationSpec = MapSpec.SocialHub(seed: 1337);
        if (!MapSpec.TryGetMapSizeWireIndex(4, 4, 4, out var mapSizeIndex))
            throw new InvalidOperationException("The City of Last Light map size has no wire index.");
        destinationSpec.Searchable[MapSpec.MapSizeSlot] = mapSizeIndex;

        return WorldTransfer.Handle(connection, bitStream, "SKYSAGA_SOCIAL_HUB_PORT",
            ClientConnected.SocialWorldUuid, ClientConnected.SocialServerUuid, destinationSpec,
            expectedSourceWorld: "home");
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinSocialHub);

        return bitStream;
    }
}
