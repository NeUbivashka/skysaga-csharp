namespace SkySaga.Game.Packets;

public class EndInteractionWithEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EndInteractionWithEntity);

        return bitStream;
    }
}
