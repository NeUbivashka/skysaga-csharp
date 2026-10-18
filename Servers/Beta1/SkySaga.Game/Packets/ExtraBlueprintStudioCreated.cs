namespace SkySaga.Game.Packets;

public class ExtraBlueprintStudioCreated : ISerializablePacket
{
    public Guid? StudioUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ExtraBlueprintStudioCreated);

        InventoryItemRef.WriteOptionalUuid(bitStream, StudioUuid);

        return bitStream;
    }
}
