namespace SkySaga.Game.Packets;

public class MailRead : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MailRead);

        return bitStream;
    }
}
