namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveCircle : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveCircle);

        return bitStream;
    }
}
