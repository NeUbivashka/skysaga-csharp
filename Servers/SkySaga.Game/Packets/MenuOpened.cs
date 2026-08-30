namespace SkySaga.Game.Packets;

public class MenuOpened : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MenuOpened);

        return bitStream;
    }
}
