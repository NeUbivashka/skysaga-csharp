namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 256, server to client. Reader: FUN_007FBFF0 -> FUN_00BB3E50(1, 0x100, ...).
///
///     PhotoUuid      1 presence bit + 4 x 32 bits
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class RefreshPhotoMetadata : ISerializablePacket
{

    public Guid? PhotoUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RefreshPhotoMetadata);

        InventoryItemRef.WriteOptionalUuid(bitStream, PhotoUuid);

        return bitStream;
    }
}
