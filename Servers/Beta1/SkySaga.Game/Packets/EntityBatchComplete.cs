namespace SkySaga.Game.Packets;

public class EntityBatchComplete : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityBatchComplete);

        return bitStream;
    }
}
