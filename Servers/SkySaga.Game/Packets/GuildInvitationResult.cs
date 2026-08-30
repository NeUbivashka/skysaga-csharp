namespace SkySaga.Game.Packets;

public class GuildInvitationResult : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GuildInvitationResult);

        return bitStream;
    }
}
