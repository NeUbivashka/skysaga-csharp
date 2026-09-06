namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 24, server to client. Reader: FUN_007FF5E0 -> FUN_00BB3E50(1, 0x18, ...).
///
/// One string, read with FUN_007EA530 - the same reader every other string in the protocol uses,
/// so WriteString covers it. Nothing else follows.
///
/// The stub sent the packet id alone, which left the client parsing a string header out of
/// whatever came next in the buffer.
/// </summary>
public class SetReportUrl : ISerializablePacket
{
    public string? Url;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetReportUrl);

        bitStream.WriteString(Url);

        return bitStream;
    }
}
