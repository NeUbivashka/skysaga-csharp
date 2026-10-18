namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveTriangle : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveTriangle);

        return bitStream;
    }
}
