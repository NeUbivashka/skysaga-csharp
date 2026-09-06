namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 127, server to client. Reader: FUN_007EC8A0 -> FUN_00BB3E50(1, 0x7F, ...).
///
///     BlueprintUuid   1 presence bit + 4 x 32 bits
///     OwnerUuid       1 presence bit + 4 x 32 bits
///     (FUN_00795690 reads nothing)
///     Record          one BlueprintRecord (FUN_007E8430)
/// </summary>
public class BlueprintRevokeResult : ISerializablePacket
{
    public Guid? BlueprintUuid;
    public Guid? OwnerUuid;
    public BlueprintRecord Record = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintRevokeResult);

        InventoryItemRef.WriteOptionalUuid(bitStream, BlueprintUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, OwnerUuid);
        Record.Encode(bitStream);

        return bitStream;
    }
}
