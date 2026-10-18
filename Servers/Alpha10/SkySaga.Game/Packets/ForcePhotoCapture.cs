namespace SkySaga.Game.Packets;

public class ForcePhotoCapture : ISerializablePacket
{
    public bool Immediate;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ForcePhotoCapture);

        bitStream.Write(Immediate);

        return bitStream;
    }
}
