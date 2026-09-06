namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 0, server to client. Reader: FUN_007FBCF0 -> FUN_00BB3E50(1, 0, ...).
///
/// Verified empty. The stub reads nothing out of the bitstream at all - it goes straight from
/// the log call to dispatching its handler with no arguments. So this class is already complete;
/// the packet id is the entire message.
/// </summary>
public class QuitGame : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QuitGame);

        return bitStream;
    }
}
