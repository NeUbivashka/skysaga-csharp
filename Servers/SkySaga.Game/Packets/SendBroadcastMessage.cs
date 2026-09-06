namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 82, server to client. Reader: FUN_007FD7A0 -> FUN_00BB3E50(1, 0x52, ...).
///
/// One string, and nothing else.
/// </summary>
public class SendBroadcastMessage : ISerializablePacket
{
    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SendBroadcastMessage);

        bitStream.WriteString(Message);

        return bitStream;
    }
}
