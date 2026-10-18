namespace SkySaga.Game.Packets;

public class DebugEnablePermission : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugEnablePermission);

        return bitStream;
    }
}
