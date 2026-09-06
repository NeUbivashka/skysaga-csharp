namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 131, server to client. Reader: FUN_007ECD00 -> FUN_00BB3E50(1, 0x83, ...).
///
///     BlueprintUuid   1 presence bit + 4 x 32 bits
///     Flag            1 bit
///     (FUN_00795690 reads nothing)
///     Record          one BlueprintRecord (FUN_007E8430)
///     Amount          32 bits, full width -> big-endian
///     Balance         32 bits, full width -> big-endian
/// </summary>
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
