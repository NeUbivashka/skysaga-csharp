namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 79, server to client. Reader: FUN_007FEDC0 -> FUN_00BB3E50(1, 0x4F, ...).
///
///     EntityId   32 bits, full width -> big-endian
///     ActionId   1 presence bit + 32 bits
///     State      ranged, max 4 -> 3 bits
/// </summary>
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
