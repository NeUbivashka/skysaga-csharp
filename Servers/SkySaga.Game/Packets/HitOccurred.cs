namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 37, server to client. Reader: FUN_007F6010 -> FUN_00BB3E50(1, 0x25, ...).
///
///     TargetEntityId   32 bits, full width -> big-endian
///     SourceEntityId   32 bits, full width -> big-endian
///     Amount           FUN_007D69B0: ranged, max 0x7FFF -> 15 bits
///     Kind             ranged, max 9 -> 4 bits
///     Unnamed          32 bits, full width -> big-endian
///     Result           FUN_007E7E70: 1 bit, and if set an 8-bit ranged value, max 0x98
///
/// The first five fields are the same opening HealOccurred and KillOccurred use. The last one held
/// this packet up for a while: FUN_007E7E70 looked like a nested structure, but it is a plain
/// optional - one bit, then the same 8-bit code CombatResult carries, read by the same
/// FUN_007E8BA0. The FUN_0077A510 call that sits between them reads nothing at all; neither it nor
/// any of its five callees touches the bitstream.
/// </summary>
public class HitOccurred : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;
    private const uint KindMax = 9;
    private const uint ResultMax = 0x98;

    public int TargetEntityId;
    public int SourceEntityId;
    public uint Amount;
    public uint Kind;
    public int Unnamed;

    /// <summary>Null writes a single clear bit.</summary>
    public uint? Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.HitOccurred);

        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);
        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Unnamed);

        if (Result is null)
        {
            bitStream.Write0();
        }
        else
        {
            bitStream.Write1();
            bitStream.WriteRanged(Math.Min(Result.Value, ResultMax), ResultMax);
        }

        return bitStream;
    }
}
