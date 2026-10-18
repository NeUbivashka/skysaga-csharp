namespace SkySaga.Game.Packets;

public class EntityDodged : ISerializablePacket
{
    private const uint DodgeKindMax = 8;
    public int EntityId;
    public uint DodgeKind;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityDodged);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(DodgeKind, DodgeKindMax), DodgeKindMax);

        return bitStream;
    }
}
