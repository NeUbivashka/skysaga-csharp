namespace SkySaga.Game.Packets;

public class RewardTrackStageBuy : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RewardTrackStageBuy);

        return bitStream;
    }
}
