namespace SkySaga.Game.Packets;

public class DebugRequestAddEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestAddEntity);

        return bitStream;
    }
}
