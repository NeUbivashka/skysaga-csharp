namespace SkySaga.Game.Packets;

public class SetUserToken : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetUserToken);

        return bitStream;
    }
}
