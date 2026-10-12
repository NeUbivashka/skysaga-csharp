namespace SkySaga.Game.Packets;

public class GroupNotification : ISerializablePacket
{
    private const uint KindMax = 0xA;

    public uint Kind;
    public Guid? GroupUuid;
    public Guid? PlayerUuid;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GroupNotification);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GroupUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, PlayerUuid);

        return bitStream;
    }
}
