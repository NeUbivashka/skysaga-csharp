namespace SkySaga.Game.Packets;

public class DebugSetShortResetProgressDeadlines : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetShortResetProgressDeadlines);

        return bitStream;
    }
}
