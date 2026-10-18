namespace SkySaga.Game.Packets;

public class DebugSetJobChallengeProgress : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetJobChallengeProgress);

        return bitStream;
    }
}
