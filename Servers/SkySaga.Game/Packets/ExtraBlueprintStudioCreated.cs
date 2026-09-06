namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 294, server to client. Reader: FUN_007F38E0 -> FUN_00BB3E50(1, 0x126, ...).
///
///     StudioUuid     1 presence bit + 4 x 32 bits
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class ExtraBlueprintStudioCreated : ISerializablePacket
{

    public Guid? StudioUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ExtraBlueprintStudioCreated);

        InventoryItemRef.WriteOptionalUuid(bitStream, StudioUuid);

        return bitStream;
    }
}
