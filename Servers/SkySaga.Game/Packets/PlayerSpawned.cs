namespace SkySaga.Game.Packets;

public class PlayerSpawned : ISerializablePacket
{
    private const byte Ordinal = 159;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        return bitStream;
    }
}
