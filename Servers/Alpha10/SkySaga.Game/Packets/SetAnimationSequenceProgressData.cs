namespace SkySaga.Game.Packets;

public class SetAnimationSequenceProgressData : ISerializablePacket
{
    public required string SequenceName;
    public required float Progress;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetAnimationSequenceProgressData);

        bitStream.WriteString(SequenceName);

        var progressBytes = BitConverter.GetBytes(Progress);
        Array.Reverse(progressBytes);
        bitStream.WriteBits(progressBytes, 32, true);

        return bitStream;
    }
}
