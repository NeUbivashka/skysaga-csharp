namespace SkySaga.Game.Packets;

public class GuildOperationResult : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildOperationResult);

        return bitStream;
    }
}
