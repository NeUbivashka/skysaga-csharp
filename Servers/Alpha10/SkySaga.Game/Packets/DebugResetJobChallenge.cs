namespace SkySaga.Game.Packets;

public class DebugResetJobChallenge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugResetJobChallenge);

        return bitStream;
    }
}
