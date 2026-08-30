namespace SkySaga.Game.Packets;

public class RewardTrackStageClaimCompleted : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RewardTrackStageClaimCompleted);

        return bitStream;
    }
}
