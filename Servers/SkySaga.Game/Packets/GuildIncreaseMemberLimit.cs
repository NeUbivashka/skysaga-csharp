namespace SkySaga.Game.Packets;

public class GuildIncreaseMemberLimit : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildIncreaseMemberLimit);

        return bitStream;
    }
}
