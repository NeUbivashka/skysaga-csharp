namespace SkySaga.Game.Packets;

public class GuildNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildNotification);

        return bitStream;
    }
}
