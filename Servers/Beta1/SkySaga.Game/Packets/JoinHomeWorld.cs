namespace SkySaga.Game.Packets;

public class JoinHomeWorld : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinHomeWorld);

        return bitStream;
    }
}
