namespace SkySaga.Game.Packets;

public class MessageOfTheDay : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MessageOfTheDay);

        return bitStream;
    }
}
