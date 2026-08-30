namespace SkySaga.Game.Packets;

public class DebugCompleteJobChallenge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugCompleteJobChallenge);

        return bitStream;
    }
}
