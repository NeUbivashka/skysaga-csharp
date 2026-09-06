namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 296, server to client. Reader: FUN_007F1E70 -> FUN_00BB3E50(1, 0x128, ...).
///
///     EntityId       32 bits, full width -> big-endian
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class EntityJumped : ISerializablePacket
{

    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityJumped);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
