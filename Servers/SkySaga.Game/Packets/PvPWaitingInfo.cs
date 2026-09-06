namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 97, server to client. Reader: FUN_00925950 -> FUN_00BB3E50(1, 0x61, ...).
///
/// One 64-bit value, read by FUN_00C715B0 as 0x40 bits with an eight-byte swap - so it goes out
/// big-endian, the 64-bit version of the same rule the 32-bit fields follow. Everything after the
/// read in that function is local formatting, not more reading.
///
/// A single wide number on a "waiting info" packet is almost certainly a deadline or a timestamp,
/// but nothing here names itself.
/// </summary>
public class PvPWaitingInfo : ISerializablePacket
{
    public ulong Value;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PvPWaitingInfo);

        WriteUInt64(bitStream, Value);

        return bitStream;
    }

    internal static void WriteUInt64(BitStream bitStream, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 64, true);
    }
}
