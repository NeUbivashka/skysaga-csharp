namespace SkySaga.Game.Packets;

public class UnlockPosition : ISerializablePacket
{
    private const byte Ordinal = 157;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        return bitStream;
    }
}
