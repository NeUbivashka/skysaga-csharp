namespace SkySaga.Game.Packets;

public class SetEntityActionState : ISerializablePacket
{
    private const uint StateMax = 4;

    public int EntityId;
    public uint? ActionId;
    public uint State;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SetEntityActionState);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionId);
        bitStream.WriteRanged(Math.Min(State, StateMax), StateMax);

        return bitStream;
    }
}
