namespace SkySaga.Game.Packets;

public class Batch : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.Batch);

        return bitStream;
    }
}
