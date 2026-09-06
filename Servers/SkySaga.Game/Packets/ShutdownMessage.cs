namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 2, server to client. Reader: FUN_00800360 -> FUN_00BB3E50(1, 2, ...).
///
///     Reason    32 bits
///     Seconds   32 bits
///
/// Both are read at full width through the FUN_006FB780 path, so both are byte-swapped on the
/// way in and have to go out big-endian - same rule as MapSpec.Seed and every other full-width
/// field in this project. The stub used to send the id alone, which left the client reading two
/// words of whatever followed in the buffer.
///
/// The handler takes the two values in this order; what they mean is not recorded anywhere we
/// can read, so the names are by role. A countdown message with a reason code and a delay is the
/// obvious shape, but do not treat the names as established.
/// </summary>
public class ShutdownMessage : ISerializablePacket
{
    public uint Reason;

    public uint Seconds;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShutdownMessage);

        WriteUInt32(bitStream, Reason);
        WriteUInt32(bitStream, Seconds);

        return bitStream;
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
