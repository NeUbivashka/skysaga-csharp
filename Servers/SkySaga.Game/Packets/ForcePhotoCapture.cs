namespace SkySaga.Game.Packets;

public class ForcePhotoCapture : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ForcePhotoCapture);

        return bitStream;
    }
}
