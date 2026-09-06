namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 313, server to client. Reader: FUN_007F55E0 -> FUN_00BB3E50(1, 0x139, ...).
///
///     Operation      ranged, max 8
///     GuildUuid      1 presence bit + 4 x 32 bits
///     Result         32 bits, full width -> big-endian
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class GuildOperationResult : ISerializablePacket
{
    private const uint OperationMax = 8;

    public uint Operation;
    public Guid? GuildUuid;
    public int Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GuildOperationResult);

        bitStream.WriteRanged(Math.Min(Operation, OperationMax), OperationMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GuildUuid);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Result);

        return bitStream;
    }
}
