namespace SkySaga.Game.Packets;

public class GuildInvite : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildInvite);

        return bitStream;
    }
}
