namespace SkySaga.Game.Packets;

public class GuildRemove : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildRemove);

        return bitStream;
    }
}
