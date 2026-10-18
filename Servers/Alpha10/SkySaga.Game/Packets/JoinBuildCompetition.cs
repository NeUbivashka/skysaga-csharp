namespace SkySaga.Game.Packets;

public class JoinBuildCompetition : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinBuildCompetition);

        return bitStream;
    }
}
