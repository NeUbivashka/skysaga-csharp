namespace SkySaga.Game.Packets;

public class EntityStartedSequentialCombo : ISerializablePacket
{
    public int EntityId;
    public uint? ComboId;
    public bool Flag;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityStartedSequentialCombo);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, ComboId);
        bitStream.Write(Flag);

        return bitStream;
    }
}
