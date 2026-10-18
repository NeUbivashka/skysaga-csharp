namespace SkySaga.Game.Packets;

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
