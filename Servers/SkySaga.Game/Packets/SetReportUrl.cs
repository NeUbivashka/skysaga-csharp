namespace SkySaga.Game.Packets;

public class SetReportUrl : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetReportUrl);

        return bitStream;
    }
}
