namespace SkySaga.Game.Packets;

public class DebugSetShortExpiryDeadlines : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetShortExpiryDeadlines);

        return bitStream;
    }
}
