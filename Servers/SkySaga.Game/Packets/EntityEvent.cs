namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 74, server to client. Reader: FUN_007F1A00 -> FUN_00BB3E50(1, 0x4A, ...).
///
///     EventKind   ranged, max 0xD0 -> 8 bits
///     EntityId    32 bits, full width -> big-endian
///     Parameter   FUN_007E7D10: 1 presence bit + 32 bits
///
/// EventKind shares its maximum with EventEffect's first field (also 0xD0), so the two almost
/// certainly index the same table of event types.
/// </summary>
public class EntityEvent : ISerializablePacket
{
    private const uint EventKindMax = 0xD0;

    public uint EventKind;

    public int EntityId;

    public uint? Parameter;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityEvent);

        bitStream.WriteRanged(Math.Min(EventKind, EventKindMax), EventKindMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Parameter);

        return bitStream;
    }
}
