namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 293, server to client. Reader: FUN_007F3F80 -> FUN_00BB3E50(1, 0x125, ...).
///
///     IslandUuid     1 presence bit + 4 x 32 bits
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class ExtraHomeIslandCreated : ISerializablePacket
{

    public Guid? IslandUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ExtraHomeIslandCreated);

        InventoryItemRef.WriteOptionalUuid(bitStream, IslandUuid);

        return bitStream;
    }
}
