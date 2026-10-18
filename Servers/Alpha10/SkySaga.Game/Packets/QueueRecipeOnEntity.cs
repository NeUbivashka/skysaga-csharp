namespace SkySaga.Game.Packets;

public class QueueRecipeOnEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QueueRecipeOnEntity);

        return bitStream;
    }
}
