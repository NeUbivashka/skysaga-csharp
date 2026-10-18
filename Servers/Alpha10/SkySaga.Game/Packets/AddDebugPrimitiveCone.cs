namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveCone : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveCone);

        return bitStream;
    }
}
