namespace SkySaga.Game.Packets;

public class ResourceCheck : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ResourceCheck);

        return bitStream;
    }
}
