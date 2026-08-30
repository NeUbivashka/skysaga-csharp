namespace SkySaga.Game.Packets;

public class EntityEvent : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityEvent);

        return bitStream;
    }
}
