namespace SkySaga.Game.Packets;

public class HealOccurred : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.HealOccurred);

        return bitStream;
    }
}
