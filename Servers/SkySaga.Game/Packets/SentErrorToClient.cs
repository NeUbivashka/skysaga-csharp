namespace SkySaga.Game.Packets;

public class SentErrorToClient : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SentErrorToClient);

        return bitStream;
    }
}
