namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 80, server to client. Reader: FUN_007F0C60 -> FUN_00BB3E50(1, 0x50, ...).
///
///     EntityId       32 bits, full width -> big-endian
///     ActionId       1 presence bit + 32 bits
///     State          ranged, max 3 -> 2 bits
///     RecoveryTime   FUN_007D67B0: ranged, max 0xA00 -> 12 bits
/// </summary>
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
