namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveSphere : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveSphere);

        return bitStream;
    }
}
