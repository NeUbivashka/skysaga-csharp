namespace SkySaga.Game.Packets;

public class TakeMailAttachment : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TakeMailAttachment);

        return bitStream;
    }
}
