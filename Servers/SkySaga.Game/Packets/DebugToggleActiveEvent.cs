namespace SkySaga.Game.Packets;

public class DebugToggleActiveEvent : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugToggleActiveEvent);

        return bitStream;
    }
}
