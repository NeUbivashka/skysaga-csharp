namespace SkySaga.Game.Packets;

public class DebugRequestAITree : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestAITree);

        return bitStream;
    }
}
