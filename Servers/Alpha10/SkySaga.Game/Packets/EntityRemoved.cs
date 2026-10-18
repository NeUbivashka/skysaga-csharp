namespace SkySaga.Game.Packets;

public class EntityRemoved : ISerializablePacket
{
    public int Id;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityRemoved);

        bitStream.Write(Id);

        return bitStream;
    }
}