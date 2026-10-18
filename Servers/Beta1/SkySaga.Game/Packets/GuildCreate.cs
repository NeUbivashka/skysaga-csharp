namespace SkySaga.Game.Packets;

public class GuildCreate : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildCreate);

        return bitStream;
    }
}
