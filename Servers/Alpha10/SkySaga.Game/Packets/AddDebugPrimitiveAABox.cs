namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveAABox : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveAABox);

        return bitStream;
    }
}
