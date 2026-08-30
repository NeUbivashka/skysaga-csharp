namespace SkySaga.Game.Packets;

public class NarrativeCompleted : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NarrativeCompleted);

        return bitStream;
    }
}
