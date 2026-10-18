namespace SkySaga.Game.Packets;

public class BlueprintShopPurchaseBlueprint : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintShopPurchaseBlueprint);

        return bitStream;
    }
}
