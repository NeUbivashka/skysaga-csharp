namespace SkySaga.Game.Packets;

public class DebugRequestDeleteEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestDeleteEntity);

        return bitStream;
    }
}
