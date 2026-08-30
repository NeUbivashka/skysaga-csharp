namespace SkySaga.Game.Packets;

public class RunActionList : ISerializablePacket
{
    private const byte Ordinal = 29;

    public required string ActionName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.WriteString(ActionName);

        return bitStream;
    }
}
