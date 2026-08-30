namespace SkySaga.Game.Packets;

public class EntityJumped : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityJumped);

        return bitStream;
    }
}
