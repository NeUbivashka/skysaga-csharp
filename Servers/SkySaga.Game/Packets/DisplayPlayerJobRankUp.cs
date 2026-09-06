namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 302, server to client. Reader: FUN_007F03F0 -> FUN_00BB3E50(1, 0x12E, ...).
///
///     PlayerUuid     1 presence bit + 4 x 32 bits
///     PlayerName     string
///     Flag           1 bit
///     JobId          1 presence bit + 32 bits
///     NewRank        8 bits, read raw with no swap
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class DisplayPlayerJobRankUp : ISerializablePacket
{
    public Guid? PlayerUuid;
    public string? PlayerName;
    public bool Flag;
    public uint? JobId;
    public byte NewRank;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.DisplayPlayerJobRankUp);

        InventoryItemRef.WriteOptionalUuid(bitStream, PlayerUuid);
        bitStream.WriteString(PlayerName);
        bitStream.Write(Flag);
        InventoryItemRef.WriteOptionalUInt32(bitStream, JobId);
        bitStream.WriteBits([NewRank], 8, true);

        return bitStream;
    }
}
