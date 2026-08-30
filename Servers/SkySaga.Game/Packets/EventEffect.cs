namespace SkySaga.Game.Packets;

public class EventEffect : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EventEffect);

        return bitStream;
    }
}
