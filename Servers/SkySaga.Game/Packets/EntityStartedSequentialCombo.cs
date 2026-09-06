namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 78, server to client. Reader: FUN_007F2A70 -> FUN_00BB3E50(1, 0x4E, ...).
///
///     EntityId   32 bits, full width -> big-endian
///     ComboId    1 presence bit + 32 bits
///     Flag       1 bit
/// </summary>
public class EntityStartedSequentialCombo : ISerializablePacket
{
    public int EntityId;
    public uint? ComboId;
    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityStartedSequentialCombo);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, ComboId);
        bitStream.Write(Flag);

        return bitStream;
    }
}
