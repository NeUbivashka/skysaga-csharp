namespace SkySaga.Game.Packets;

public class CollectDailyReward : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CollectDailyReward);

        return bitStream;
    }
}
