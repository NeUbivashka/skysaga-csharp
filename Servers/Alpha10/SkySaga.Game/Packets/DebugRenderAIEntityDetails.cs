namespace SkySaga.Game.Packets;

public class DebugRenderAIEntityDetails : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRenderAIEntityDetails);

        return bitStream;
    }
}
