namespace SkySaga.Game.Packets;

public class CharacterCreationResponse : ISerializablePacket
{
    private const uint ResultMax = 4;

    public uint Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CharacterCreationResponse);

        bitStream.WriteRanged(Math.Min(Result, ResultMax), ResultMax);

        return bitStream;
    }
}
