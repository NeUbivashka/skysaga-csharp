namespace SkySaga.Game.Packets;

public class DebugAddNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAddNotification);

        return bitStream;
    }
}
