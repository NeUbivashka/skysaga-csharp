namespace SkySaga.Game.Packets;

public class MenuClosed : ISerializablePacket
{
    private const byte Ordinal = 110;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        return bitStream;
    }
}
