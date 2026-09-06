namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 337, server to client. Reader: FUN_007FB500 -> FUN_00BB3E50(1, 0x151, ...).
///
///     PromotionId    32 bits, full width -> big-endian
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class PromotionAddedToIsland : ISerializablePacket
{
    public int PromotionId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PromotionAddedToIsland);

        InventoryItemRef.WriteUInt32(bitStream, (uint)PromotionId);

        return bitStream;
    }
}
