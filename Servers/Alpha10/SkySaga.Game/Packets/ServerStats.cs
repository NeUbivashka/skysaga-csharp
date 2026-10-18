namespace SkySaga.Game.Packets;

public class ServerStats : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ServerStats);

        return bitStream;
    }
}
