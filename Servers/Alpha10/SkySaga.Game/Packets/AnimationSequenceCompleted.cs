namespace SkySaga.Game.Packets;

public class AnimationSequenceCompleted : ISerializablePacket
{
    public required string SequenceName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AnimationSequenceCompleted);

        bitStream.WriteString(SequenceName);

        return bitStream;
    }
}
