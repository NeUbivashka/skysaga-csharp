namespace SkySaga.Game.Packets;

public class DebugDeactivateJobChallenge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugDeactivateJobChallenge);

        return bitStream;
    }
}
