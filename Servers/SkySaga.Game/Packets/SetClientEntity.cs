namespace SkySaga.Game.Packets;

public class SetClientEntity : ISerializablePacket
{
    private const byte Ordinal = 158;

    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.Write(EntityId);

        return bitStream;
    }
}