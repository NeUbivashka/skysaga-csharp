namespace SkySaga.Game.Packets;

public class DebugRenderInfoAllAIEntities : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRenderInfoAllAIEntities);

        return bitStream;
    }
}
