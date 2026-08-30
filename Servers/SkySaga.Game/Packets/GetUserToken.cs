namespace SkySaga.Game.Packets;

public class GetUserToken : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GetUserToken);

        return bitStream;
    }
}
