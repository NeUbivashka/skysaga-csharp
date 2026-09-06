namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 36, server to client. Reader: FUN_007F6960 -> FUN_00BB3E50(1, 0x24, ...).
///
///     TargetEntityId   32 bits, full width -> big-endian
///     SourceEntityId   32 bits, full width -> big-endian
///     Item             one InventoryItemRef (FUN_007E9440)
///     Kind             ranged, max 9 -> 4 bits
///
/// Same two-entity-ids opening as HitOccurred and HealOccurred, and the same four-bit trailer.
/// </summary>
public class KillOccurred : ISerializablePacket
{
    private const uint KindMax = 9;

    public int TargetEntityId;

    public int SourceEntityId;

    public InventoryItemRef Item = new();

    public uint Kind;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.KillOccurred);

        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);

        Item.Encode(bitStream);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        return bitStream;
    }
}
