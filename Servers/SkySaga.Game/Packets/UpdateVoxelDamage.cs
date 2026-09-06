namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 32, server to client. Reader: FUN_00801C20 -> FUN_00BB3E50(1, 0x20, ...).
///
///     DamageId    32 bits, full width -> big-endian
///     Amount      FUN_007D76B0: ranged, max 0x7FFF -> 15 bits
///     EntityId    32 bits, full width -> big-endian
///     SourceId    32 bits, full width -> big-endian
///     Position    FUN_007D66A0: X 17 bits, Y 15 bits biased by -0x3FC0, Z 17 bits
///     Timestamp   FUN_00C715B0: 64 bits -> big-endian
///
/// This one sat unfinished for a while on FUN_00C715B0, which turns out to be nothing exotic:
/// 0x40 bits with an eight-byte swap, the 64-bit sibling of the 32-bit full-width reads. The rest
/// of that function formats a local value and reads nothing more.
///
/// Position is the same reader PlayerSpawned, EventEffect and AoETriggered use - 64 units per
/// world unit, narrow biased Y. Do not substitute the entity-sync encoding.
/// </summary>
public class UpdateVoxelDamage : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;

    private const uint PositionHorizontalMax = 0x10000;
    private const uint PositionVerticalMax = 0x7F80;
    private const int PositionVerticalBias = 0x3FC0;
    private const float PositionScale = 64f;

    public int DamageId;
    public uint Amount;
    public int EntityId;
    public int SourceId;
    public Vector3 Position;
    public ulong Timestamp;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.UpdateVoxelDamage);

        InventoryItemRef.WriteUInt32(bitStream, (uint)DamageId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceId);

        WriteHorizontal(bitStream, Position.X);
        WriteVertical(bitStream, Position.Y);
        WriteHorizontal(bitStream, Position.Z);

        PvPWaitingInfo.WriteUInt64(bitStream, Timestamp);

        return bitStream;
    }

    private static void WriteHorizontal(BitStream bitStream, float value)
    {
        bitStream.WriteBits(
            BitConverter.GetBytes((int)(value * PositionScale)),
            32 - Util.NumBitsRequiredUInt32(PositionHorizontalMax),
            true);
    }

    private static void WriteVertical(BitStream bitStream, float value)
    {
        var units = Math.Clamp((int)(value * PositionScale) + PositionVerticalBias, 0, (int)PositionVerticalMax);

        bitStream.WriteBits(
            BitConverter.GetBytes(units),
            32 - Util.NumBitsRequiredUInt32(PositionVerticalMax),
            true);
    }
}
