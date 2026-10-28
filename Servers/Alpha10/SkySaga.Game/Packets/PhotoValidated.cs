namespace SkySaga.Game.Packets;

public class PhotoValidated : ISerializablePacket
{
    private const uint CaptureIdShortMax = 0x7F;

    public uint CaptureId;
    public Guid? PhotoUuid;
    public Guid? UploadToken;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PhotoValidated);

        if (CaptureId <= CaptureIdShortMax)
        {
            bitStream.Write0();
            bitStream.WriteRanged(CaptureId, CaptureIdShortMax);
        }
        else
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, CaptureId);
        }

        InventoryItemRef.WriteOptionalUuid(bitStream, PhotoUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, UploadToken);

        return bitStream;
    }
}
