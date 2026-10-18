namespace SkySaga.Game.Packets;

public class EntityJumped : ISerializablePacket
{
    public int EntityId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityJumped);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        return bitStream;
    }
}
