namespace SkySaga.Game.Packets;

public class ApplyImpulse : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ApplyImpulse);

        return bitStream;
    }
}
