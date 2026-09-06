namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 85, server to client. Reader: FUN_007F4980 -> FUN_00BB3E50(1, 0x55, ...).
///
/// Verified empty - the stub reads nothing from the bitstream before its log call. The packet id
/// is the whole message, which fits the name: this asks, and SetUserToken (86, client to server)
/// answers.
/// </summary>
public class GetUserToken : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GetUserToken);

        return bitStream;
    }
}
