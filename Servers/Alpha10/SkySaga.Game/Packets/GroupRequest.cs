namespace SkySaga.Game.Packets;

public class GroupRequest : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupRequest);

        return bitStream;
    }
}
