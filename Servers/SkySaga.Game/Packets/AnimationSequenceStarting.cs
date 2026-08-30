namespace SkySaga.Game.Packets;

public class AnimationSequenceStarting : ISerializablePacket
{
    public required string SequenceName;
    public required float DurationSeconds;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AnimationSequenceStarting);

        bitStream.WriteString(SequenceName);

        var durationBytes = BitConverter.GetBytes(DurationSeconds);
        Array.Reverse(durationBytes);
        bitStream.WriteBits(durationBytes, 32, true);

        return bitStream;
    }
}
