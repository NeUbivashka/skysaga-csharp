namespace SkySaga.Game.Packets;

public class RequestServerProfiles : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestServerProfiles);

        return bitStream;
    }
}
