namespace SkySaga.Game.Packets;

public class ActivateJobChallenge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ActivateJobChallenge);

        return bitStream;
    }
}
