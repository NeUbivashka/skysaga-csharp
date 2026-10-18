namespace SkySaga.Game.Packets;

public class GameQueueRequest : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GameQueueRequest);

        return bitStream;
    }
}
