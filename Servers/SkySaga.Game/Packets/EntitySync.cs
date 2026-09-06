namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 154, server to client. Reader: FUN_007F2E70 -> FUN_00BB3E50(1, 0x9A, ...).
///
///     Id         32 bits, full width -> big-endian
///     SyncData   FUN_007D7490: escape bit, then an 18- or 32-bit length, then that many bits
///
/// Identical tail to EntityAdd, and it had the identical bug plus one more.
///
/// 1. The escape bit before the length was missing. FUN_007D7490 reads a flag first: set means a
///    32-bit length follows, clear means 18 bits. Without it the client takes the top bit of our
///    18-bit length as the flag and then reads 18 bits that are the remaining 17 length bits plus
///    the first bit of the payload - so the length it computes is roughly double the real one.
///
/// 2. WriteBits was called without alignBitsToRight. Every other call in the project passes true;
///    the two-argument overload leaves the trailing partial byte aligned the other way, so even
///    the bits that were written landed wrong.
///
/// This is the continuous state stream - it goes out for every changed entity, every tick - so
/// unlike EntityAdd, which only fires on spawn, this one was corrupting gameplay traffic
/// constantly.
/// </summary>
public class EntitySync : ISerializablePacket
{
    private const byte Ordinal = 154;

    /// <summary>Above this the length needs the escape form.</summary>
    private const uint ShortLengthMax = 0x20000;

    public int Id;

    public required BitStream SyncData;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.Write(Id);

        var syncDataBits = SyncData.GetNumberOfBitsUsed();

        WriteSyncDataLength(bitStream, syncDataBits);

        if (syncDataBits > 0)
            bitStream.Write(SyncData);

        return bitStream;
    }

    /// <summary>Same encoding as EntityAdd - the escape bit, then the length it selected.</summary>
    private static void WriteSyncDataLength(BitStream bitStream, uint bits)
    {
        if (bits <= ShortLengthMax)
        {
            bitStream.Write0();

            bitStream.WriteBits(
                BitConverter.GetBytes(bits),
                32 - Util.NumBitsRequiredUInt32(ShortLengthMax),
                true);

            return;
        }

        bitStream.Write1();

        bitStream.WriteBits(BitConverter.GetBytes(bits), 32, true);
    }
}
