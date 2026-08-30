namespace SkySaga.Game.Packets;

public class AoETriggered : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AoETriggered);

        return bitStream;
    }
}
