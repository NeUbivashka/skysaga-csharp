namespace SkySaga.Game.Packets;

public class DebugDamageEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugDamageEntity);

        return bitStream;
    }
}
