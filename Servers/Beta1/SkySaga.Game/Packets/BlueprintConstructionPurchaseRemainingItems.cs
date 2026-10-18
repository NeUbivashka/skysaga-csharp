namespace SkySaga.Game.Packets;

public class BlueprintConstructionPurchaseRemainingItems : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintConstructionPurchaseRemainingItems);

        return bitStream;
    }
}
