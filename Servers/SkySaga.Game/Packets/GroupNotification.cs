namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 323, server to client. Reader: FUN_007F4C80 -> FUN_00BB3E50(1, 0x143, ...).
///
///     Kind           ranged, max 0xA
///     GroupUuid      1 presence bit + 4 x 32 bits
///     PlayerUuid     1 presence bit + 4 x 32 bits
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class GroupNotification : ISerializablePacket
{
    private const uint KindMax = 0xA;

    public uint Kind;
    public Guid? GroupUuid;
    public Guid? PlayerUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GroupNotification);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GroupUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, PlayerUuid);

        return bitStream;
    }
}
