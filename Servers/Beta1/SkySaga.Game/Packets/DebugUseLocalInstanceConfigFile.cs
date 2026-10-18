namespace SkySaga.Game.Packets;

public class DebugUseLocalInstanceConfigFile : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugUseLocalInstanceConfigFile);

        return bitStream;
    }
}
