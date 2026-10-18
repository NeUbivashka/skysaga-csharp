namespace SkySaga.Game.Packets;

public class EntityEvent : ISerializablePacket
{
    private const uint EventKindMax = 0xD0;

    public uint EventKind;

    public int EntityId;

    public uint? Parameter;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityEvent);

        bitStream.WriteRanged(Math.Min(EventKind, EventKindMax), EventKindMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Parameter);

        return bitStream;
    }
}
