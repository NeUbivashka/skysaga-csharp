namespace SkySaga.Game.Packets;

public class DebugDumpSyncDataStats : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugDumpSyncDataStats);

        return bitStream;
    }
}
