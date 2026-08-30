namespace SkySaga.Game.Packets;

public class XPGained : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.XPGained);

        return bitStream;
    }
}
