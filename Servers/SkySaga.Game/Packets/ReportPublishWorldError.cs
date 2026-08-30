namespace SkySaga.Game.Packets;

public class ReportPublishWorldError : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ReportPublishWorldError);

        return bitStream;
    }
}
