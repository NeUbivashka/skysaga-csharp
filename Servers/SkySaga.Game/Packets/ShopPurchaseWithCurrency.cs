namespace SkySaga.Game.Packets;

public class ShopPurchaseWithCurrency : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShopPurchaseWithCurrency);

        return bitStream;
    }
}
