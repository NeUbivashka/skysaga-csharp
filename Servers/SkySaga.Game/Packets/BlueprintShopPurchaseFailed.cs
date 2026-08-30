namespace SkySaga.Game.Packets;

public class BlueprintShopPurchaseFailed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintShopPurchaseFailed);

        return bitStream;
    }
}
