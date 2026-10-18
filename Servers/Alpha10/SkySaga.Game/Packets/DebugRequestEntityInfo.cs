namespace SkySaga.Game.Packets;

public class DebugRequestEntityInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestEntityInfo);

        return bitStream;
    }
}
