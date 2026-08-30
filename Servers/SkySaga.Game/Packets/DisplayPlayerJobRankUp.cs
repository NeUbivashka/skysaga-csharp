namespace SkySaga.Game.Packets;

public class DisplayPlayerJobRankUp : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DisplayPlayerJobRankUp);

        return bitStream;
    }
}
