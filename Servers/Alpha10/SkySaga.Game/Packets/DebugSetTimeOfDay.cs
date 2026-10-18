namespace SkySaga.Game.Packets;

public class DebugSetTimeOfDay : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetTimeOfDay);

        return bitStream;
    }
}
