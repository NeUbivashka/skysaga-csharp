namespace SkySaga.Game.Packets;

public class RefreshPhotoMetadata : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RefreshPhotoMetadata);

        return bitStream;
    }
}
