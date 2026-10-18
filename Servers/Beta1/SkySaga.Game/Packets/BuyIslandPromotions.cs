namespace SkySaga.Game.Packets;

public class BuyIslandPromotions : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BuyIslandPromotions);

        return bitStream;
    }
}
