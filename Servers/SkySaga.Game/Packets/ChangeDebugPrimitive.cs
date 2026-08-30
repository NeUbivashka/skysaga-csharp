namespace SkySaga.Game.Packets;

public class ChangeDebugPrimitive : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ChangeDebugPrimitive);

        return bitStream;
    }
}
