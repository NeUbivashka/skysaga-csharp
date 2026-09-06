namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 57, server to client. Reader: FUN_007F0020 -> FUN_00BB3E50(1, 0x39, ...).
///
///     EntityId   32 bits, full width -> big-endian
///     Item       one InventoryItemRef (FUN_007E9440)
/// </summary>
public class DismantleFailed : ISerializablePacket
{
    public int EntityId;

    public InventoryItemRef Item = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DismantleFailed);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        Item.Encode(bitStream);

        return bitStream;
    }
}
