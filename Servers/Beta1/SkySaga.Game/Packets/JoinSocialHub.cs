namespace SkySaga.Game.Packets;

public class JoinSocialHub : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.JoinSocialHub);

        return bitStream;
    }
}
