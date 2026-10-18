namespace SkySaga.Game.Packets;

public class NPCShopSell : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NPCShopSell);

        return bitStream;
    }
}
