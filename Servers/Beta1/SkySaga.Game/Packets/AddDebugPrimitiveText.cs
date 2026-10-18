namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveText : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveText);

        return bitStream;
    }
}
