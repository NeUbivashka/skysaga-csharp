namespace SkySaga.Game.Packets;

public class MarkChallengeComplete : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MarkChallengeComplete);

        return bitStream;
    }
}
