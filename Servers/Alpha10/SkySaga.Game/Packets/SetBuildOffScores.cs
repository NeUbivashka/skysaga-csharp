namespace SkySaga.Game.Packets;

public class SetBuildOffScores : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetBuildOffScores);

        return bitStream;
    }
}
