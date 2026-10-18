namespace SkySaga.Game.Packets;

public class ReturnEntityInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ReturnEntityInfo);

        return bitStream;
    }
}
