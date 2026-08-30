namespace SkySaga.Game.Packets;

public class EntityLanded : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityLanded);

        return bitStream;
    }
}
