namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 125, server to client. Reader: FUN_007ED270 -> FUN_00BB3E50(1, 0x7D, ...).
///
///     BlueprintUuid   1 presence bit + 4 x 32 bits
///     (FUN_00795690 reads nothing - both it and FUN_00795160 are container plumbing)
///     Record          one BlueprintRecord (FUN_007E8430)
///     Flag            1 bit
/// </summary>
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
