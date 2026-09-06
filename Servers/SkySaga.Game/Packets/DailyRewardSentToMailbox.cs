namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 103, server to client. Reader: FUN_007EFCB0 -> FUN_00BB3E50(1, 0x67, ...).
///
/// A single 32-bit field, read at full width through the FUN_006FB780 path, so it goes out
/// big-endian. Nothing else in the body.
/// </summary>
public class DailyRewardSentToMailbox : ISerializablePacket
{
    public int MailId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.DailyRewardSentToMailbox);

        InventoryItemRef.WriteUInt32(bitStream, (uint)MailId);

        return bitStream;
    }
}
