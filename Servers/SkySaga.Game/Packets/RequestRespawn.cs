namespace SkySaga.Game.Packets;

public class RequestRespawn : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestRespawn);

        return bitStream;
    }
}
