namespace SkySaga.Game.Packets;

public class DismantleFailed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DismantleFailed);

        return bitStream;
    }
}
