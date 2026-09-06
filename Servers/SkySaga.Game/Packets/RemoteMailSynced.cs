namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 148, server to client. Reader: FUN_007FC340 -> FUN_00BB3E50(1, 0x94, ...).
///
/// Verified empty - the stub reads nothing from the bitstream. The packet id is the whole message,
/// which is all a "your mailbox finished syncing" notification needs to be.
/// </summary>
public class RemoteMailSynced : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RemoteMailSynced);

        return bitStream;
    }
}
