namespace SkySaga.Game.Packets;

public class DebugRequestDeleteEntitiesWithComponent : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestDeleteEntitiesWithComponent);

        return bitStream;
    }
}
