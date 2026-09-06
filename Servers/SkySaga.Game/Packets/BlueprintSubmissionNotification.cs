namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 118, server to client. Reader: FUN_007EDB40 -> FUN_00BB3E50(1, 0x76, ...).
///
/// One string, and nothing else.
/// </summary>
public class BlueprintSubmissionNotification : ISerializablePacket
{
    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintSubmissionNotification);

        bitStream.WriteString(Message);

        return bitStream;
    }
}
