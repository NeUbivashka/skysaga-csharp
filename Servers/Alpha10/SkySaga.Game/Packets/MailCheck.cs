namespace SkySaga.Game.Packets;

public class MailCheck : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MailCheck);

        return bitStream;
    }
}
