namespace SkySaga.Game.Packets;

public class GroupRequestAccept : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupRequestAccept);

        return bitStream;
    }
}
