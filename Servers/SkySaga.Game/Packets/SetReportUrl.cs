namespace SkySaga.Game.Packets;

public class SetReportUrl : ISerializablePacket
{
    public string? Url;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetReportUrl);

        bitStream.WriteString(Url);

        return bitStream;
    }
}
