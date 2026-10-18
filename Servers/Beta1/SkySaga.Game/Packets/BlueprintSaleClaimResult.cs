namespace SkySaga.Game.Packets;

public class BlueprintSaleClaimResult : ISerializablePacket
{
    public Guid? BlueprintUuid;
    public bool Flag;
    public BlueprintRecord Record = new();
    public int Amount;
    public int Balance;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintSaleClaimResult);

        InventoryItemRef.WriteOptionalUuid(bitStream, BlueprintUuid);
        bitStream.Write(Flag);
        Record.Encode(bitStream);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Amount);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Balance);

        return bitStream;
    }
}
