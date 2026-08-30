namespace SkySaga.Game.Packets;

public class KillOccurred : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.KillOccurred);

        return bitStream;
    }
}
