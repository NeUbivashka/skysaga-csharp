namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveLine : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveLine);

        return bitStream;
    }
}
