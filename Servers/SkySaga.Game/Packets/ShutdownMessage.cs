namespace SkySaga.Game.Packets;

public class ShutdownMessage : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShutdownMessage);

        return bitStream;
    }
}
