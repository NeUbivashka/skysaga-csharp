namespace SkySaga.Game.Packets;

public class GuildOperationResult : ISerializablePacket
{
    private const uint OperationMax = 8;

    public uint Operation;
    public Guid? GuildUuid;
    public int Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.GuildOperationResult);

        bitStream.WriteRanged(Math.Min(Operation, OperationMax), OperationMax);
        InventoryItemRef.WriteOptionalUuid(bitStream, GuildUuid);
        InventoryItemRef.WriteUInt32(bitStream, (uint)Result);

        return bitStream;
    }
}
