namespace SkySaga.Game.Packets;

/// <summary>
/// Reader: FUN_007F1140 -> FUN_00BB3E50(1, 0x99, ...)
///
///   NameHash    FUN_007EA4B0   presence bit + 32 bits
///   Id          inline         32 bits
///   ParentId    FUN_007E7D10   presence bit + 32 bits
///   SyncData    FUN_007D7490   escape bit, then 18- or 32-bit length
///               FUN_00EA6640   that many bits
///
/// The field order and all three 32-bit values were already right. What was missing is one bit.
/// FUN_007D7490 reads a flag before the length:
///
///     if (ReadBit()) width = 32 - NumBitsRequired(0xFFFFFFFF);   // 32 bits
///     else           width = 32 - NumBitsRequired(0x20000);      // 18 bits
///
/// The server wrote the 18-bit length with no flag in front of it. The arithmetic on any packet
/// in the log shows it: the Airship entity came to 219 bits, which is
/// 16 + 1 + 32 + 32 + 1 + 18 + 119 - exactly one bit short of what the client consumes.
///
/// The consequence is not a one-bit rounding error. The client takes the top bit of the length
/// as the flag, then reads 18 bits that are the remaining 17 length bits plus the first bit of
/// the payload, so the length it ends up with is roughly double the real one. For the player
/// entity, 1950 bits of sync data were being read as about 3900 - past the end of the packet.
/// Every EntityAdd was landing as garbage: the NPC, the airship, TimeOfDay, the player and all
/// eighteen inventory items.
///
/// The 32-bit branch is read through FUN_007187F0, the ranged predicate, so that length stays
/// little-endian - unlike the full-width fields above it, which go out big-endian via
/// bitStream.Write.
/// </summary>
public class EntityAdd : ISerializablePacket
{
    private const byte Ordinal = 153;

    /// <summary>Above this the length needs the escape form.</summary>
    private const uint ShortLengthMax = 0x20000;

    public uint? NameHash;

    public int Id;

    public int? ParentId;

    public required BitStream SyncData;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.WriteOptional(NameHash, (value) =>
        {
            bitStream.Write((int)value);
        });

        bitStream.Write(Id);

        bitStream.WriteOptional(ParentId, bitStream.Write);

        var syncDataBits = SyncData.GetNumberOfBitsUsed();

        WriteSyncDataLength(bitStream, syncDataBits);

        if (syncDataBits > 0)
            bitStream.Write(SyncData);

        return bitStream;
    }

    /// <summary>
    /// The escape bit the client reads before the length, then the length itself in whichever
    /// width that bit selected.
    /// </summary>
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
