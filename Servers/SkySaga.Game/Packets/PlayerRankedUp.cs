namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 301, server to client. Reader: FUN_007FA820 -> FUN_00BB3E50(1, 0x12D, ...).
///
///     EntityId       32 bits, full width -> big-endian
///     JobId          1 presence bit + 32 bits
///     NewRank        8 bits, read raw with no swap
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class PlayerRankedUp : ISerializablePacket
{
    public int EntityId;
    public uint? JobId;
    public byte NewRank;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PlayerRankedUp);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, JobId);
        bitStream.WriteBits([NewRank], 8, true);

        return bitStream;
    }
}
