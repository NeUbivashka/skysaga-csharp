namespace SkySaga.Game.Packets;

public class BlueprintShopPurchaseStudioScroll : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintShopPurchaseStudioScroll);

        return bitStream;
    }
}
