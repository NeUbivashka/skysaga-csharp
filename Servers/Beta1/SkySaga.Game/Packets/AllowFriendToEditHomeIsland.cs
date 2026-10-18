namespace SkySaga.Game.Packets;

public class AllowFriendToEditHomeIsland : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AllowFriendToEditHomeIsland);

        return bitStream;
    }
}
