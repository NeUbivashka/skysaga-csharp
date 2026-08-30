namespace SkySaga.Game.Packets;

public class EntityDodged : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityDodged);

        return bitStream;
    }
}
