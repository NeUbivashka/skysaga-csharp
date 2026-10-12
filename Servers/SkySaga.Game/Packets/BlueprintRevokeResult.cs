namespace SkySaga.Game.Packets;

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
