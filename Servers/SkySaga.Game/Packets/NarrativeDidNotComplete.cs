namespace SkySaga.Game.Packets;

public class NarrativeDidNotComplete : ISerializablePacket
{
    public ushort NarrativeIndex;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.NarrativeDidNotComplete);

        bitStream.WriteBits(BitConverter.GetBytes(NarrativeIndex), 16, true);

        return bitStream;
    }
}
