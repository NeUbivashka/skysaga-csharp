namespace SkySaga.Game.Packets;

public class RemoveDebugPrimitive : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemoveDebugPrimitive);

        return bitStream;
    }
}
