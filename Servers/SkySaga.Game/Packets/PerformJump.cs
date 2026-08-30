namespace SkySaga.Game.Packets;

public class PerformJump : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PerformJump);

        return bitStream;
    }
}
