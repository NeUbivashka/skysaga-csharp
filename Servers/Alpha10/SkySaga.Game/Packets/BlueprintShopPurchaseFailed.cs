namespace SkySaga.Game.Packets;

public class BlueprintShopPurchaseFailed : ISerializablePacket
{
    public Guid? BlueprintUuid;
    public BlueprintRecord Record = new();
    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintShopPurchaseFailed);

        InventoryItemRef.WriteOptionalUuid(bitStream, BlueprintUuid);
        Record.Encode(bitStream);
        bitStream.Write(Flag);

        return bitStream;
    }
}
