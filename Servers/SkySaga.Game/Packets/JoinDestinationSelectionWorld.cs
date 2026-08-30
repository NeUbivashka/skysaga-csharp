namespace SkySaga.Game.Packets;

public class JoinDestinationSelectionWorld : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinDestinationSelectionWorld);

        return bitStream;
    }
}
