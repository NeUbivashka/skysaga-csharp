namespace SkySaga.Game.Packets;

public class NarrativeDidNotComplete : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NarrativeDidNotComplete);

        return bitStream;
    }
}
