namespace SkySaga.Game.Packets;

public class RefreshPhotoMetadata : ISerializablePacket
{
    public Guid? PhotoUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RefreshPhotoMetadata);

        InventoryItemRef.WriteOptionalUuid(bitStream, PhotoUuid);

        return bitStream;
    }
}
