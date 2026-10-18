namespace SkySaga.Game.Packets;

public class DebugNeverQueueForGames : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugNeverQueueForGames);

        return bitStream;
    }
}
