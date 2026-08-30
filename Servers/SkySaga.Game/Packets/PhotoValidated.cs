namespace SkySaga.Game.Packets;

public class PhotoValidated : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PhotoValidated);

        return bitStream;
    }
}
