namespace SkySaga.Game.Packets;

public class EntityActionRecovery : ISerializablePacket
{
    private const uint StateMax = 3;
    private const uint RecoveryTimeMax = 0xA00;

    public int EntityId;
    public uint? ActionId;
    public uint State;
    public uint RecoveryTime;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.EntityActionRecovery);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionId);
        bitStream.WriteRanged(Math.Min(State, StateMax), StateMax);
        bitStream.WriteRanged(Math.Min(RecoveryTime, RecoveryTimeMax), RecoveryTimeMax);

        return bitStream;
    }
}
