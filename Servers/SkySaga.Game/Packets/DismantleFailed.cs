namespace SkySaga.Game.Packets;

public class DismantleFailed : ISerializablePacket
{
    public int EntityId;

    public InventoryItemRef Item = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DismantleFailed);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        Item.Encode(bitStream);

        return bitStream;
    }
}
