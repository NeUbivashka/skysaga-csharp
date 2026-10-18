namespace SkySaga.Game.Packets;

public class PhotoDelete : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PhotoDelete);

        return bitStream;
    }
}
