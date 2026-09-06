namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 267, server to client. Reader: FUN_007F8E80 -> FUN_00BB3E50(1, 0x10B, ...).
///
///     Result     FUN_007D7630: an escape bit, then 7 bits, or 32 if the bit is set
///     PhotoUuid  1 presence bit + 4 x 32 bits
///     OwnerUuid  1 presence bit + 4 x 32 bits
/// </summary>
public class PhotoValidated : ISerializablePacket
{
    private const uint ResultShortMax = 0x7F;

    public uint Result;
    public Guid? PhotoUuid;
    public Guid? OwnerUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PhotoValidated);

        if (Result <= ResultShortMax)
        {
            bitStream.Write0();
            bitStream.WriteRanged(Result, ResultShortMax);
        }
        else
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, Result);
        }

        InventoryItemRef.WriteOptionalUuid(bitStream, PhotoUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, OwnerUuid);

        return bitStream;
    }
}
