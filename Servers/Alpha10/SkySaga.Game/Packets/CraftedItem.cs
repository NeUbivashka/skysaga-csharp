namespace SkySaga.Game.Packets;

public class CraftedItem : ISerializablePacket
{
    public int EntityId;
    public int ItemId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.CraftedItem);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)ItemId);

        return bitStream;
    }
}
