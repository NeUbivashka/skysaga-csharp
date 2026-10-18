namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveQuad : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveQuad);

        return bitStream;
    }
}
