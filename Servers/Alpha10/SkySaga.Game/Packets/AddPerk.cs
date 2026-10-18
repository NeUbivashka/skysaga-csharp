namespace SkySaga.Game.Packets;

public class AddPerk : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AddPerk);

        return bitStream;
    }
}
