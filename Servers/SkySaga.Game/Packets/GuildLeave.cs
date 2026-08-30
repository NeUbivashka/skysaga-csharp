namespace SkySaga.Game.Packets;

public class GuildLeave : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildLeave);

        return bitStream;
    }
}
