namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 281, server to client. Reader: FUN_007F15E0 -> FUN_00BB3E50(1, 0x119, ...).
///
///     EntityId       32 bits, full width -> big-endian
///     DodgeKind      ranged, max 8
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class EntityDodged : ISerializablePacket
{
    private const uint DodgeKindMax = 8;
    public int EntityId;
    public uint DodgeKind;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityDodged);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(DodgeKind, DodgeKindMax), DodgeKindMax);

        return bitStream;
    }
}
