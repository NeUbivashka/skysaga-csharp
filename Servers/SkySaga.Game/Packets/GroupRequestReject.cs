namespace SkySaga.Game.Packets;

public class GroupRequestReject : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupRequestReject);

        return bitStream;
    }
}
