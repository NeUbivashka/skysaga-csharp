namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 299, server to client. Reader: FUN_007FA160 -> FUN_00BB3E50(1, 0x12B, ...).
///
///     Reason         ranged, max 2
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class PlayerKicked : ISerializablePacket
{
    private const uint ReasonMax = 2;
    public uint Reason;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PlayerKicked);

        bitStream.WriteRanged(Math.Min(Reason, ReasonMax), ReasonMax);

        return bitStream;
    }
}
