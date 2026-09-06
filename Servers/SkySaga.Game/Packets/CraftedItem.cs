namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 84, server to client. Reader: FUN_007EF0C0 -> FUN_00BB3E50(1, 0x54, ...).
///
///     EntityId   32 bits, full width -> big-endian
///     ItemId     32 bits, full width -> big-endian
/// </summary>
public class CraftedItem : ISerializablePacket
{
    public int EntityId;
    public int ItemId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CraftedItem);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)ItemId);

        return bitStream;
    }
}
