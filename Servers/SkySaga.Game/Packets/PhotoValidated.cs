namespace SkySaga.Game.Packets;

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
