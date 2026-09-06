namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 307, server to client. Reader: FUN_007FCDA0 -> FUN_00BB3E50(1, 0x133, ...).
///
///     ErrorCode      32 bits, full width -> big-endian
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class ReportPublishWorldError : ISerializablePacket
{
    public int ErrorCode;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ReportPublishWorldError);

        InventoryItemRef.WriteUInt32(bitStream, (uint)ErrorCode);

        return bitStream;
    }
}
