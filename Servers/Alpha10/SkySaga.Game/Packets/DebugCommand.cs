namespace SkySaga.Game.Packets;

public class DebugCommand : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugCommand);

        return bitStream;
    }
}
