namespace SkySaga.Game.Packets;

public class RemovePerk : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RemovePerk);

        return bitStream;
    }
}
