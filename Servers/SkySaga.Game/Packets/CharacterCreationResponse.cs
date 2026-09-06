namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 163, server to client. Reader: FUN_007EDE70 -> FUN_00BB3E50(1, 0xA3, ...).
///
/// One ranged field, maximum 4, so 3 bits. That is the whole packet - a small result code, which
/// is what a response to character creation would be.
/// </summary>
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
