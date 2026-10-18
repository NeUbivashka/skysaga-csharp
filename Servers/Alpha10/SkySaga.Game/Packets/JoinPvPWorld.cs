namespace SkySaga.Game.Packets;

public class JoinPvPWorld : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinPvPWorld);

        return bitStream;
    }
}
