namespace SkySaga.Game.Packets;

public class DebugDumpHeapToCSV : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugDumpHeapToCSV);

        return bitStream;
    }
}
