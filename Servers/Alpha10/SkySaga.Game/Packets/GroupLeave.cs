namespace SkySaga.Game.Packets;

public class GroupLeave : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupLeave);

        return bitStream;
    }
}
