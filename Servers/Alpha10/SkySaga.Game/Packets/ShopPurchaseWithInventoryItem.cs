namespace SkySaga.Game.Packets;

public class ShopPurchaseWithInventoryItem : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShopPurchaseWithInventoryItem);

        return bitStream;
    }
}
