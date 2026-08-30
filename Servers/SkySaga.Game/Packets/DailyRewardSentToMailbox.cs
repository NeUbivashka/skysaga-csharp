namespace SkySaga.Game.Packets;

public class DailyRewardSentToMailbox : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DailyRewardSentToMailbox);

        return bitStream;
    }
}
