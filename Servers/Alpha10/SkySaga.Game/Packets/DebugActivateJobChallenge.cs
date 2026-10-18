namespace SkySaga.Game.Packets;

public class DebugActivateJobChallenge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugActivateJobChallenge);

        return bitStream;
    }
}
