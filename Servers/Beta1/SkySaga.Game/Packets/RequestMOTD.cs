namespace SkySaga.Game.Packets;

public class RequestMOTD : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestMOTD);

        return bitStream;
    }
}
