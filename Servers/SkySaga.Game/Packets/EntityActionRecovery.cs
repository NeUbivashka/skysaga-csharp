namespace SkySaga.Game.Packets;

public class EntityActionRecovery : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityActionRecovery);

        return bitStream;
    }
}
