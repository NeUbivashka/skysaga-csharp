namespace SkySaga.Game.Packets;

public class EnableDebugRendering : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EnableDebugRendering);

        return bitStream;
    }
}
