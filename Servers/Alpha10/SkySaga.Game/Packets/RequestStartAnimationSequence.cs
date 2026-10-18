namespace SkySaga.Game.Packets;

public class RequestStartAnimationSequence : ISerializablePacket
{
    public required string SequenceName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestStartAnimationSequence);

        bitStream.WriteString(SequenceName);

        return bitStream;
    }
}
