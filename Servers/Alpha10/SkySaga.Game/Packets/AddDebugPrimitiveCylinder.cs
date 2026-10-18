namespace SkySaga.Game.Packets;

public class AddDebugPrimitiveCylinder : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddDebugPrimitiveCylinder);

        return bitStream;
    }
}
