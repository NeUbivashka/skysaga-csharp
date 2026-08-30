namespace SkySaga.Game.Packets;

public class ProjectileImpacted : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ProjectileImpacted);

        return bitStream;
    }
}
