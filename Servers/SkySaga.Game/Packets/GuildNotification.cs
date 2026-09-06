namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 314, server to client. Reader: FUN_007F50E0 -> FUN_00BB3E50(1, 0x13A, ...).
///
///     Kind           ranged, max 6
///     GuildUuid      1 presence bit + 4 x 32 bits
///     PlayerName     string
///     Message        string
///     TargetUuid     1 presence bit + 4 x 32 bits
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class GuildNotification : ISerializablePacket
{
    private const uint KindMax = 6;

    public uint Kind;
    public Guid? GuildUuid;
    public string? PlayerName;
    public string? Message;
    public Guid? TargetUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GuildNotification);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GuildUuid);
        bitStream.WriteString(PlayerName);
        bitStream.WriteString(Message);
        InventoryItemRef.WriteOptionalUuid(bitStream, TargetUuid);

        return bitStream;
    }
}
