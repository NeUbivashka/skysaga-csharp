namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 1, server to client. Reader: FUN_007FDB00 -> FUN_00BB3E50(1, 1, ...).
///
///     ErrorCode    32 bits
///     Detail       32 bits
///     Title        string
///     Message      string
///
/// The two numbers are full-width reads through the FUN_006FB780 path, so they go out
/// big-endian. The two strings use FUN_007EA530, the same reader as every other string in the
/// protocol, so WriteString handles them.
///
/// The stub sent the id and nothing else, so the client read four fields' worth of stale buffer
/// whenever this was used. Field names are by role - the logger's name pointers do not survive
/// decompilation in this stub - but the count, order and widths come straight from the reader.
/// </summary>
public class SentErrorToClient : ISerializablePacket
{
    public uint ErrorCode;

    public uint Detail;

    public string? Title;

    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SentErrorToClient);

        WriteUInt32(bitStream, ErrorCode);
        WriteUInt32(bitStream, Detail);

        bitStream.WriteString(Title);
        bitStream.WriteString(Message);

        return bitStream;
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
