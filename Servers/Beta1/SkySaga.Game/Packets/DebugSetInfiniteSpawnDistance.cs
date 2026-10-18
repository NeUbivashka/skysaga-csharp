namespace SkySaga.Game.Packets;

public class DebugSetInfiniteSpawnDistance : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetInfiniteSpawnDistance);

        return bitStream;
    }
}
