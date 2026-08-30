namespace SkySaga.Game.Packets;

public class DebugForceRefreshJobChallenges : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugForceRefreshJobChallenges);

        return bitStream;
    }
}
