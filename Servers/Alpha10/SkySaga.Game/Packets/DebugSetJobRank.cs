namespace SkySaga.Game.Packets;

public class DebugSetJobRank : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetJobRank);

        return bitStream;
    }
}
