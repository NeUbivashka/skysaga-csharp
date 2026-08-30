namespace SkySaga.Game.Packets;

public class HitOccurred : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.HitOccurred);

        return bitStream;
    }
}
